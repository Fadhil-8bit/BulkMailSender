using System.Text;
using System.Text.Json;
using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class SendResultsModel : PageModel
{
    private readonly EmailSendQueueService _queueService;
    private readonly ILogger<SendResultsModel> _logger;

    public SendResultsModel(EmailSendQueueService queueService, ILogger<SendResultsModel> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    public SendSummary? Results { get; set; }

    public void OnGet()
    {
        // Try to load from job queue first
        var jobIdJson = HttpContext.Session.GetString("CurrentJobId");
        if (!string.IsNullOrEmpty(jobIdJson))
        {
            try
            {
                var jobId = JsonSerializer.Deserialize<string>(jobIdJson);
                if (!string.IsNullOrEmpty(jobId))
                {
                    var job = _queueService.GetJob(jobId);
                    if (job != null)
                    {
                        Results = job.Results;
                        
                        // Persistent fallback: save to session in case service clears it
                        HttpContext.Session.SetString("SendResults", JsonSerializer.Serialize(Results));
                        return;
                    }
                }
            }
            catch { }
        }

        // Fallback: load from session
        var json = HttpContext.Session.GetString("SendResults");
        if (!string.IsNullOrEmpty(json))
        {
            try { Results = JsonSerializer.Deserialize<SendSummary>(json); }
            catch { }
        }
    }

    public IActionResult OnGetJobStatus()
    {
        OnGet();

        bool isCompleted = true;
        string status = "Completed";

        var jobIdJson = HttpContext.Session.GetString("CurrentJobId");
        if (!string.IsNullOrEmpty(jobIdJson))
        {
            try
            {
                var jobId = JsonSerializer.Deserialize<string>(jobIdJson);
                if (!string.IsNullOrEmpty(jobId))
                {
                     var job = _queueService.GetJob(jobId);
                     if (job != null)
                     {
                         status = job.Status.ToString();
                         isCompleted = (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed || job.Status == JobStatus.Cancelled);
                     }
                }
            }
            catch {}
        }

        if (Results == null)
        {
             return new JsonResult(new { sentCount = 0, failedCount = 0, total = 0, status, isCompleted });
        }

        return new JsonResult(new 
        { 
            sentCount = Results.Sent.Count, 
            failedCount = Results.Failed.Count, 
            total = Results.Sent.Count + Results.Failed.Count + Results.Skipped.Count,
            status,
            isCompleted
        });
    }

    public IActionResult OnPostDownloadFailed()
    {
        // Load results
        OnGet();

        if (Results == null || !Results.Failed.Any())
        {
            TempData["ErrorMessage"] = "No failed emails to download.";
            return RedirectToPage("/SendResults");
        }

        // Generate CSV
        var csv = new StringBuilder();
        csv.AppendLine("Debtor Code,Error Message,Retry Count,Timestamp");

        foreach (var failed in Results.Failed)
        {
            var debtor = failed.DebtorCode?.Replace("\"", "\"\"") ?? "";
            var reason = failed.Reason?.Replace("\"", "\"\"") ?? "";
            csv.AppendLine($"\"{debtor}\",\"{reason}\",{failed.RetryCount},\"{failed.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"FailedEmails_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        _logger.LogInformation("Downloaded failed emails CSV: {Count} records", Results.Failed.Count);

        return File(bytes, "text/csv", fileName);
    }

    public IActionResult OnPostRetryFailed()
    {
        // Load current results
        OnGet();

        if (Results == null || !Results.Failed.Any())
        {
            TempData["ErrorMessage"] = "No failed emails to retry.";
            return RedirectToPage("/SendResults");
        }

        // Load original job data
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        var uploadResultJson = HttpContext.Session.GetString("UploadResult");
        var templateJson = HttpContext.Session.GetString("EmailTemplate");
        var smtpJson = HttpContext.Session.GetString("SmtpSettings");
        
        if (string.IsNullOrEmpty(recipientsJson) || string.IsNullOrEmpty(uploadResultJson) || string.IsNullOrEmpty(templateJson))
        {
             TempData["ErrorMessage"] = "Cannot retry. Original session data missing.";
             return RedirectToPage("/SendResults");
        }

        try
        {
            var allRecipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson) ?? new List<DebtorRecipient>();
            var uploadResult = JsonSerializer.Deserialize<UploadResult>(uploadResultJson);
            var template = JsonSerializer.Deserialize<SavedTemplate>(templateJson);
            var smtp = string.IsNullOrEmpty(smtpJson) 
                ? new EmailSettings() 
                : JsonSerializer.Deserialize<EmailSettings>(smtpJson);

            // Filter recipients to only failed debtor codes
            var failedDebtorCodes = Results.Failed.Select(f => f.DebtorCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var retryRecipients = allRecipients
                .Where(r => failedDebtorCodes.Contains(r.DebtorCode))
                .ToList();

            if (!retryRecipients.Any())
            {
                TempData["ErrorMessage"] = "No recipients found for failed debtor codes.";
                return RedirectToPage("/SendResults");
            }

            // Create new job for retry
            var retryJob = new EmailSendJob
            {
                Recipients = retryRecipients,
                UploadResult = uploadResult,
                Template = template,
                SmtpSettings = smtp,
                TotalEmails = retryRecipients.GroupBy(r => r.DebtorCode).Count()
            };

            var jobId = _queueService.EnqueueJob(retryJob);
            HttpContext.Session.SetString("CurrentJobId", JsonSerializer.Serialize(jobId));

            // Reset session results immediately so UI polling sees zero/fresh state
            HttpContext.Session.SetString("SendResults", JsonSerializer.Serialize(retryJob.Results));

            _logger.LogInformation("Retry job {JobId} created for {Count} failed debtor codes", jobId, failedDebtorCodes.Count);

            TempData["SuccessMessage"] = $"Retry job created for {failedDebtorCodes.Count} failed emails. Redirecting to preview...";
            return RedirectToPage("/Preview");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create retry job");
            TempData["ErrorMessage"] = $"Failed to create retry job: {ex.Message}";
            return RedirectToPage("/SendResults");
        }
    }
}
