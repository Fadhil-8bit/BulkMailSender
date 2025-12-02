using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Mail;
using System.Net;

namespace BulkMailSender.Pages;

public class PreviewModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PreviewModel> _logger;

    public PreviewModel(IConfiguration configuration, ILogger<PreviewModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
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

        var results = new SendSummary();

        foreach (var group in grouped)
        {
            var debtorCode = group.Key;
            var recipientList = group.ToList();

            // find attachments for debtor
            var debtorAttach = uploadResult?.DebtorAttachments.FirstOrDefault(d => string.Equals(d.DebtorCode, debtorCode, StringComparison.OrdinalIgnoreCase));

            // determine attachments based on template type
            var attachmentsToAdd = new List<string>();
            if (template != null && debtorAttach != null)
            {
                if (template.TemplateType == TemplateType.SoaInv)
                {
                    attachmentsToAdd.AddRange(debtorAttach.InvoiceFiles.Select(f => f.FilePath));
                    attachmentsToAdd.AddRange(debtorAttach.StatementFiles.Select(f => f.FilePath));
                }
                else
                {
                    attachmentsToAdd.AddRange(debtorAttach.OverdueFiles.Select(f => f.FilePath));
                }
            }

            if (!attachmentsToAdd.Any())
            {
                results.Skipped.Add(new PerDebtorResult { DebtorCode = debtorCode, Reason = "No attachments found for this debtor and template type." });
                continue;
            }

            // compose message
            try
            {
                using var msg = new MailMessage();
                msg.From = new MailAddress(smtp.FromEmail ?? smtp.Username, smtp.FromName ?? "Bulk Mail Sender");

                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.To)) msg.To.Add(r.Email);
                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.Cc)) msg.CC.Add(r.Email);
                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.Bcc)) msg.Bcc.Add(r.Email);

                // Replace placeholders
                var org = recipientList.FirstOrDefault()?.OrganizationName ?? "{organization name}";
                var notes = recipientList.FirstOrDefault()?.Notes ?? "{notes}";
                var debtorPlaceholder = debtorCode;

                var subject = template != null ? template.Subject : string.Empty;
                var body = template != null ? template.Body : string.Empty;
                subject = subject.Replace("{organization name}", org).Replace("{notes}", notes).Replace("{debtor code}", debtorPlaceholder);
                body = body.Replace("{organization name}", org).Replace("{notes}", notes).Replace("{debtor code}", debtorPlaceholder);

                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = false;

                // attach files
                foreach (var path in attachmentsToAdd.Distinct())
                {
                    if (System.IO.File.Exists(path))
                    {
                        msg.Attachments.Add(new Attachment(path));
                    }
                }

                using var client = new SmtpClient(smtp.Host, smtp.Port)
                {
                    EnableSsl = smtp.EnableSsl,
                    Timeout = smtp.TimeoutSeconds * 1000,
                    Credentials = string.IsNullOrWhiteSpace(smtp.Username) ? null : new NetworkCredential(smtp.Username, smtp.Password)
                };

                await client.SendMailAsync(msg);

                results.Sent.Add(new PerDebtorResult { DebtorCode = debtorCode, To = recipientList.Where(x => x.Label == EmailLabel.To).Select(x => x.Email).ToList(), Message = "Sent" });
            }
            catch (Exception ex)
            {
                results.Failed.Add(new PerDebtorResult { DebtorCode = debtorCode, Reason = ex.Message });
                _logger.LogError(ex, "Failed sending to debtor {Debtor}", debtorCode);
            }
        }

        // Save results to session and redirect to results page
        HttpContext.Session.SetString("SendResults", JsonSerializer.Serialize(results));
        return RedirectToPage("/SendResults");
    }
}

public class DebtorAttachmentsSummary
{
    public int InvCount { get; set; }
    public int SoaCount { get; set; }
    public int OdCount { get; set; }
    public int OtherCount { get; set; }
}

public class SendSummary
{
    public List<PerDebtorResult> Sent { get; set; } = new();
    public List<PerDebtorResult> Failed { get; set; } = new();
    public List<PerDebtorResult> Skipped { get; set; } = new();
}

public class PerDebtorResult
{
    public string DebtorCode { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
