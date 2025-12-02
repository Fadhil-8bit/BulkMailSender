namespace BulkMailSender.Models;

public class FileAttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string CustomCode { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public string FileSizeFormatted
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            else if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F2} KB";
            else
                return $"{FileSize / (1024.0 * 1024.0):F2} MB";
        }
    }
}
