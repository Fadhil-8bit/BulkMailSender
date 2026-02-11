using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BulkMailSender.Models;
using System.Text.Json;

namespace BulkMailSender.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public bool HasRecipients { get; set; }
        public bool HasUploads { get; set; }
        public int RecipientCount { get; set; }

        public void OnGet()
        {
            // Check Recipients
            var recipientsJson = HttpContext.Session.GetString("Recipients");
            HasRecipients = !string.IsNullOrEmpty(recipientsJson);
            
            if (HasRecipients)
            {
                try
                {
                    // Peek at count if possible, or just trust it exists
                    // Doing a light parse to get count might be nice for the dashboard
                    // Assuming list of DebtorRecipient
                     var recipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson);
                     RecipientCount = recipients?.Count ?? 0;
                }
                catch
                {
                    // Ignore parsing errors for dashboard summary
                    RecipientCount = 0;
                }
            }

            // Check Uploads
            var uploadResultJson = HttpContext.Session.GetString("UploadResult");
            HasUploads = !string.IsNullOrEmpty(uploadResultJson);
        }

        public IActionResult OnPostReset()
        {
            HttpContext.Session.Clear();
            return RedirectToPage();
        }
    }
}
