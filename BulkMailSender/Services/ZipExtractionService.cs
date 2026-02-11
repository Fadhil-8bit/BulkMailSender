using BulkMailSender.Models;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace BulkMailSender.Services;

public class ZipExtractionService : IZipExtractionService
{
    private static readonly Regex _debtorStyleRegex = new Regex("^[A-Z0-9]+-[A-Z0-9]+$", RegexOptions.Compiled);
    private static readonly Regex _fileNamePatternRegex = new Regex(@"^(.+?)\s+(INV|SOA|OD|OTHER)\s+(\d{4,6})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger<ZipExtractionService> _logger;

    public ZipExtractionService(ILogger<ZipExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<UploadResult> ExtractAndCategorizeAsync(IFormFile zipFile, string webRootPath, List<string>? validDebtorCodes = null, CancellationToken cancellationToken = default)
    {
        var result = new UploadResult();
        string tempZipPath = string.Empty;
        string extractPath = string.Empty;

        try
        {
            if (zipFile == null || zipFile.Length == 0)
            {
                result.Success = false;
                result.Message = "No file uploaded or file is empty.";
                return result;
            }

            if (!zipFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.Message = "Only ZIP files are allowed.";
                return result;
            }

            _logger.LogInformation($"Starting upload: {zipFile.FileName}, Size: {zipFile.Length} bytes ({zipFile.Length / (1024.0 * 1024.0):F2} MB)");

            // Create uploads directory
            var uploadsPath = Path.Combine(webRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Create unique extraction path for this upload
            extractPath = Path.Combine(uploadsPath, Guid.NewGuid().ToString());
            
            // Set the extraction path in the result
            result.ExtractionPath = extractPath;

            // Create extraction directory
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);

            // IMPORTANT: Save ZIP to disk first to avoid stream timeout issues
            tempZipPath = Path.Combine(extractPath, $"temp_{Guid.NewGuid()}.zip");
            _logger.LogInformation($"Saving uploaded file to: {tempZipPath}");

            // Copy uploaded file to disk with buffering
            using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                await zipFile.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            _logger.LogInformation($"File saved successfully. Size on disk: {new FileInfo(tempZipPath).Length} bytes");

            // Extract ZIP file from disk (not from stream)
            _logger.LogInformation("Starting ZIP extraction...");
            await Task.Run(() => 
            {
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int extractedCount = 0;
                    _logger.LogInformation($"Total entries to extract: {totalEntries}");

                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Prevent Zip Slip vulnerability
                        var destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                        if (!destinationPath.StartsWith(Path.GetFullPath(extractPath), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            // It's a directory
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            // It's a file, ensure directory exists
                            var parentDir = Path.GetDirectoryName(destinationPath);
                            if (parentDir != null)
                            {
                                Directory.CreateDirectory(parentDir);
                            }
                            entry.ExtractToFile(destinationPath, true);
                        }

                        extractedCount++;
                        if (extractedCount % 50 == 0)
                        {
                            _logger.LogInformation($"Extracted {extractedCount} of {totalEntries} entries...");
                        }
                    }
                }
            });

            _logger.LogInformation($"ZIP file extracted to {extractPath}");

            // Delete temporary ZIP file
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
                _logger.LogInformation("Temporary ZIP file deleted");
            }

            // Get all files from extraction path (including subdirectories)
            var allFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith(".") && !f.EndsWith(".zip")) // Exclude hidden files and zip files
                .ToList();

            result.TotalFiles = allFiles.Count;
            _logger.LogInformation($"Found {result.TotalFiles} files to categorize");

            // Categorize files by debtor code
            var debtorDict = new Dictionary<string, DebtorAttachment>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                var fullFileName = Path.GetFileName(filePath);

                // Pattern: {DebtorCode} {DocType} {CustomCode}
                // Example: "3000-AT502 INV 12345" or "3000-AT015 OTHER 987654"
                // Supports: INV, SOA, OD, OTHER (for other documents)
                var match = _fileNamePatternRegex.Match(fileName);

                if (match.Success)
                {
                    var debtorCodeRaw = match.Groups[1].Value.Trim();
                    var debtorCode = debtorCodeRaw.ToUpperInvariant();
                    var docType = match.Groups[2].Value.ToUpperInvariant();
                    var customCode = match.Groups[3].Value;

                    // Validate debtor code style
                    if (!_debtorStyleRegex.IsMatch(debtorCode))
                    {
                        result.UncategorizedFiles++;
                        result.Errors.Add($"Invalid debtor code style in file '{fullFileName}': '{debtorCodeRaw}'. Expected format like 3000-AT502.");
                        _logger.LogWarning("Invalid debtor code style: {Debtor} in file {File}", debtorCodeRaw, fullFileName);
                        continue; // skip categorization for this file
                    }

                    // Check if debtor code is in the valid list (if provided)
                    if (validDebtorCodes != null && validDebtorCodes.Count > 0 && !validDebtorCodes.Contains(debtorCode, StringComparer.OrdinalIgnoreCase))
                    {
                        result.MissingRecipientFiles++;
                        result.Errors.Add($"File found for debtor '{debtorCode}' but this debtor is not in the recipient list: '{fullFileName}'");
                        _logger.LogWarning("Debtor code mismatch: {Debtor} in file {File} not found in recipients list", debtorCode, fullFileName);
                        continue; // skip categorization
                    }

                    if (!debtorDict.ContainsKey(debtorCode))
                    {
                        debtorDict[debtorCode] = new DebtorAttachment
                        {
                            DebtorCode = debtorCode
                        };
                    }

                    var debtor = debtorDict[debtorCode];
                    var fileInfo = new FileAttachmentInfo
                    {
                        FileName = fullFileName,
                        FilePath = filePath,
                        DocumentType = docType,
                        CustomCode = customCode,
                        FileSize = new FileInfo(filePath).Length
                    };

                    switch (docType)
                    {
                        case "INV":
                            debtor.InvoiceFile = filePath;
                            debtor.InvoiceFiles.Add(fileInfo);
                            break;
                        case "SOA":
                            debtor.StatementFile = filePath;
                            debtor.StatementFiles.Add(fileInfo);
                            break;
                        case "OD":
                            debtor.OverdueFile = filePath;
                            debtor.OverdueFiles.Add(fileInfo);
                            break;
                        case "OTHER":
                            debtor.OtherFiles.Add(fileInfo);
                            break;
                    }

                    debtor.AllAttachments.Add(filePath);
                    debtor.AllAttachmentDetails.Add(fileInfo);
                    result.CategorizedFiles++;
                }
                else
                {
                    // File doesn't match expected pattern
                    result.UncategorizedFiles++;
                    result.Errors.Add($"Unmatched file: {fullFileName}");
                    _logger.LogWarning($"File does not match naming convention: {fileName}");
                }
            }

            result.DebtorAttachments = debtorDict.Values.OrderBy(d => d.DebtorCode).ToList();
            result.Success = true;
            result.Message = $"Successfully extracted {result.TotalFiles} files. " +
                           $"Categorized: {result.CategorizedFiles}, " +
                           $"Uncategorized: {result.UncategorizedFiles}, " +
                           $"Debtors found: {result.DebtorAttachments.Count}";

            _logger.LogInformation(result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error extracting ZIP file: {ex.Message}";
            result.Errors.Add(ex.ToString());
            _logger.LogError(ex, "Error during ZIP extraction");

            // Clean up temp file on error
            if (!string.IsNullOrEmpty(tempZipPath) && File.Exists(tempZipPath))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch { }
            }
        }

        return result;
    }

    public async Task CleanupExtractedFilesAsync(string extractPath)
    {
        try
        {
            if (Directory.Exists(extractPath))
            {
                await Task.Run(() => Directory.Delete(extractPath, true));
                _logger.LogInformation($"Cleaned up extraction path: {extractPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up extraction path: {extractPath}");
        }
    }
}
