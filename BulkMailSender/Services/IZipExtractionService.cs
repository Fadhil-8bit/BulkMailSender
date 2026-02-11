using BulkMailSender.Models;
using System.Threading;

namespace BulkMailSender.Services;

public interface IZipExtractionService
{
    Task<UploadResult> ExtractAndCategorizeAsync(IFormFile zipFile, string webRootPath, List<string>? validDebtorCodes = null, CancellationToken cancellationToken = default);
    Task CleanupExtractedFilesAsync(string extractPath);
}
