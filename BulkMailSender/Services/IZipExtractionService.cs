using BulkMailSender.Models;

namespace BulkMailSender.Services;

public interface IZipExtractionService
{
    Task<UploadResult> ExtractAndCategorizeAsync(IFormFile zipFile, string extractPath);
    Task CleanupExtractedFilesAsync(string extractPath);
}
