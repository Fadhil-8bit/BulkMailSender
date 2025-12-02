using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class PreviewModel : PageModel
{
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
}

public class DebtorAttachmentsSummary
{
    public int InvCount { get; set; }
    public int SoaCount { get; set; }
    public int OdCount { get; set; }
    public int OtherCount { get; set; }
}
