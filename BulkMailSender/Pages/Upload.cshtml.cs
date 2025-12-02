using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace BulkMailSender.Pages;

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

    public void OnGet()
    {
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

    [RequestSizeLimit(524288000)] // 500 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000, ValueLengthLimit = 524288000)]
    public async Task<IActionResult> OnPostAsync()
    {
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

            // Create uploads directory
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Create unique extraction path for this upload
            var extractPath = Path.Combine(uploadsPath, Guid.NewGuid().ToString());

            // Extract and categorize files
            UploadResult = await _zipExtractionService.ExtractAndCategorizeAsync(ZipFile, extractPath);

            if (UploadResult.Success)
            {
                // Store data in SESSION (not TempData) to avoid header size limits
                HttpContext.Session.SetString("ExtractionPath", extractPath);
                HttpContext.Session.SetString("UploadResult", JsonSerializer.Serialize(UploadResult));
                
                _logger.LogInformation($"Successfully processed {UploadResult.TotalFiles} files for {UploadResult.DebtorAttachments.Count} debtors");
            }
            else
            {
                // Clean up on failure
                await _zipExtractionService.CleanupExtractedFilesAsync(extractPath);
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
