using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class SendResultsModel : PageModel
{
    public SendSummary? Results { get; set; }

    public void OnGet()
    {
        var json = HttpContext.Session.GetString("SendResults");
        if (!string.IsNullOrEmpty(json))
        {
            try { Results = JsonSerializer.Deserialize<SendSummary>(json); }
            catch { }
        }
    }
}
