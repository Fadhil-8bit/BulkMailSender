using System.Text.Json;
using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class SendResultsModel : PageModel
{
    private readonly EmailSendQueueService _queueService;

    public SendResultsModel(EmailSendQueueService queueService)
    {
        _queueService = queueService;
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

                        // Also save to session for backward compatibility
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
}
