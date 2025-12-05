using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BulkMailSender.Services;

namespace BulkMailSender.Pages;

public class PreviewModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PreviewModel> _logger;
    private readonly EmailSendQueueService _queueService;

    public PreviewModel(
        IConfiguration configuration,
        ILogger<PreviewModel> logger,
        EmailSendQueueService queueService)
    {
        _configuration = configuration;
        _logger = logger;
        _queueService = queueService;
    }

    public bool HasUploadData { get; set; }

    public SavedTemplate? Template { get; set; }

    public List<IGrouping<string, DebtorRecipient>> GroupedRecipients { get; set; } = new();

    public Dictionary<string, DebtorAttachmentsSummary> AttachmentsSummary { get; set; } = new();

    public void OnGet()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));

        // Load template
        var tmplJson = HttpContext.Session.GetString("EmailTemplate");
        if (!string.IsNullOrEmpty(tmplJson))
        {
            try { Template = JsonSerializer.Deserialize<SavedTemplate>(tmplJson); } catch { }
        }

        // Load recipients and group
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        var recipients = new List<DebtorRecipient>();
        if (!string.IsNullOrEmpty(recipientsJson))
        {
            try { recipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson) ?? new List<DebtorRecipient>(); } catch { }
        }
        GroupedRecipients = recipients.GroupBy(r => r.DebtorCode).OrderBy(g => g.Key).ToList();

        // Build attachments summary from UploadResult if available
        var uploadResultJson = HttpContext.Session.GetString("UploadResult");
        if (!string.IsNullOrEmpty(uploadResultJson))
        {
            try
            {
                var result = JsonSerializer.Deserialize<UploadResult>(uploadResultJson);
                if (result?.DebtorAttachments != null)
                {
                    foreach (var d in result.DebtorAttachments)
                    {
                        AttachmentsSummary[d.DebtorCode] = new DebtorAttachmentsSummary
                        {
                            InvCount = d.InvoiceFiles.Count,
                            SoaCount = d.StatementFiles.Count,
                            OdCount = d.OverdueFiles.Count,
                            OtherCount = d.OtherFiles.Count
                        };
                    }
                }
            }
            catch { }
        }
    }

    // API endpoint for polling send progress
    public IActionResult OnGetSendProgress()
    {
        var jobIdJson = HttpContext.Session.GetString("CurrentJobId");
        if (string.IsNullOrEmpty(jobIdJson))
        {
            return new JsonResult(new { status = "idle" });
        }

        var jobId = JsonSerializer.Deserialize<string>(jobIdJson);
        if (string.IsNullOrEmpty(jobId))
        {
            return new JsonResult(new { status = "idle" });
        }

        var job = _queueService.GetJob(jobId);
        if (job == null)
        {
            return new JsonResult(new { status = "idle" });
        }

        var response = new
        {
            status = job.Status.ToString().ToLower(),
            totalEmails = job.TotalEmails,
            sentCount = job.SentCount,
            failedCount = job.FailedCount,
            skippedCount = job.SkippedCount,
            currentDebtor = job.CurrentDebtor
        };

        return new JsonResult(response);
    }

    public async Task<IActionResult> OnPostSendAsync()
    {
        // Load data
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        var uploadResultJson = HttpContext.Session.GetString("UploadResult");
        var templateJson = HttpContext.Session.GetString("EmailTemplate");

        if (string.IsNullOrEmpty(recipientsJson) || string.IsNullOrEmpty(uploadResultJson) || string.IsNullOrEmpty(templateJson))
        {
            TempData["SendError"] = "Missing recipients, attachments, or template. Ensure Upload, Recipients and Template steps are completed.";
            return RedirectToPage("/Preview");
        }

        var recipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson) ?? new List<DebtorRecipient>();
        var uploadResult = JsonSerializer.Deserialize<UploadResult>(uploadResultJson);
        var template = JsonSerializer.Deserialize<SavedTemplate>(templateJson);

        // Load SMTP settings from session or config
        SmtpSettings smtp = null!;
        var smtpJson = HttpContext.Session.GetString("SmtpSettings");
        if (!string.IsNullOrEmpty(smtpJson))
        {
            try { smtp = JsonSerializer.Deserialize<SmtpSettings>(smtpJson)!; }
            catch { }
        }
        if (smtp == null || string.IsNullOrWhiteSpace(smtp.Host))
        {
            smtp = _configuration.GetSection("SmtpDefault").Get<SmtpSettings>() ?? new SmtpSettings();
        }

        var grouped = recipients.GroupBy(r => r.DebtorCode).OrderBy(g => g.Key).ToList();

        // Create and enqueue background job
        var job = new EmailSendJob
        {
            Recipients = recipients,
            UploadResult = uploadResult,
            Template = template,
            SmtpSettings = smtp,
            TotalEmails = grouped.Count
        };

        var jobId = _queueService.EnqueueJob(job);

        // Store job ID in session for progress polling
        HttpContext.Session.SetString("CurrentJobId", JsonSerializer.Serialize(jobId));

        _logger.LogInformation("Job {JobId} created and enqueued with {Count} emails", jobId, job.TotalEmails);

        // Return immediately - job will be processed in background
        // Stay on preview page and let JavaScript poll for progress
        return RedirectToPage("/Preview");
    }
}

public class SendProgress
{
    public string Status { get; set; } = "idle"; // idle, sending, completed
    public int TotalEmails { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public string CurrentDebtor { get; set; } = "";
}

public class DebtorAttachmentsSummary
{
    public int InvCount { get; set; }
    public int SoaCount { get; set; }
    public int OdCount { get; set; }
    public int OtherCount { get; set; }
}
