using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class RecipientsModel : PageModel
{
    private readonly ILogger<RecipientsModel> _logger;

    public RecipientsModel(ILogger<RecipientsModel> logger)
    {
        _logger = logger;
    }

    public string? ExtractionPath { get; set; }
    public bool HasUploadData { get; set; }

    public void OnGet()
    {
        // Get extraction path from session
        ExtractionPath = HttpContext.Session.GetString("ExtractionPath");
        HasUploadData = !string.IsNullOrEmpty(ExtractionPath);

        if (!HasUploadData)
        {
            _logger.LogWarning("Recipients page accessed without upload data in session");
        }
    }
}
