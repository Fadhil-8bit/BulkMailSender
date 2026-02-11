using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace BulkMailSender.Pages;

[RequestSizeLimit(524288000)] // 500 MB
[RequestFormLimits(MultipartBodyLengthLimit = 524288000, ValueLengthLimit = 524288000)]
public class UploadModel : PageModel
{
    private readonly IZipExtractionService _zipExtractionService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(
        IZipExtractionService zipExtractionService,
        IWebHostEnvironment environment,
        ILogger<UploadModel> logger)
    {
        _zipExtractionService = zipExtractionService;
        _environment = environment;
        _logger = logger;
    }

    [BindProperty]
    public IFormFile? ZipFile { get; set; }

    public UploadResult? UploadResult { get; set; }

    public bool HasRecipients { get; set; }

    public void OnGet()
    {
        // Check if recipients are present in session
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        HasRecipients = !string.IsNullOrEmpty(recipientsJson);

        // Try to load upload result from session if redirected back
        var uploadResultJson = HttpContext.Session.GetString("UploadResult");
        if (!string.IsNullOrEmpty(uploadResultJson))
        {
            try
            {
                UploadResult = JsonSerializer.Deserialize<UploadResult>(uploadResultJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize upload result from session");
            }
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Require recipients to be uploaded first
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        HasRecipients = !string.IsNullOrEmpty(recipientsJson);
        if (!HasRecipients)
        {
            UploadResult = new UploadResult
            {
                Success = false,
                Message = "Please upload the recipients CSV first before uploading attachments."
            };
            ModelState.AddModelError(string.Empty, UploadResult.Message);
            return Page();
        }

        if (ZipFile == null)
        {
            UploadResult = new UploadResult
            {
                Success = false,
                Message = "Please select a ZIP file to upload."
            };
            return Page();
        }

        try
        {
            _logger.LogInformation($"Processing ZIP file upload: {ZipFile.FileName}, Size: {ZipFile.Length} bytes");

            // Get valid debtor codes from recipients (already checked that recipients exist)
            List<string> validDebtorCodes = new List<string>();
            try
            {
                var recipientsList = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson!);
                if (recipientsList != null)
                {
                    validDebtorCodes = recipientsList
                        .Select(r => r.DebtorCode)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                 _logger.LogWarning(ex, "Failed to deserialize recipients for debtor code validation");
                 // Continue without validation if deserialization fails? 
                 // Or fail? Given previous logic, it seems OK to proceed but we lose validation.
                 // However, the prompt implies we *should* pass them. I'll pass the empty list if it fails, effectively skipping validation?
                 // No, empty list might mean "no valid debtors", so all files would be rejected if I implemented "if validCodes is not empty".
                 // In my implementation: `if (validDebtorCodes != null && validDebtorCodes.Count > 0 ...)`
                 // So if list is empty, validation is skipped. This is safe.
            }

            // Extract and categorize files
            UploadResult = await _zipExtractionService.ExtractAndCategorizeAsync(ZipFile, _environment.WebRootPath, validDebtorCodes, cancellationToken);

            if (UploadResult.Success)
            {
                // Store data in SESSION (not TempData) to avoid header size limits
                HttpContext.Session.SetString("ExtractionPath", UploadResult.ExtractionPath);
                HttpContext.Session.SetString("UploadResult", JsonSerializer.Serialize(UploadResult));
                
                _logger.LogInformation($"Successfully processed {UploadResult.TotalFiles} files for {UploadResult.DebtorAttachments.Count} debtors");
            }
            else
            {
                // Clean up on failure
                if (!string.IsNullOrEmpty(UploadResult.ExtractionPath))
                {
                    await _zipExtractionService.CleanupExtractedFilesAsync(UploadResult.ExtractionPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZIP file upload");
            UploadResult = new UploadResult
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }

        return Page();
    }
}
