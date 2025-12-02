namespace BulkMailSender.Models;

public class DebtorAttachment
{
    public string DebtorCode { get; set; } = string.Empty;
    
    // Legacy single file properties (kept for backward compatibility)
    public string? InvoiceFile { get; set; }
    public string? StatementFile { get; set; }
    public string? OverdueFile { get; set; }
    
    // New: Support multiple files per category with details
    public List<FileAttachmentInfo> InvoiceFiles { get; set; } = new();
    public List<FileAttachmentInfo> StatementFiles { get; set; } = new();
    public List<FileAttachmentInfo> OverdueFiles { get; set; } = new();
    public List<FileAttachmentInfo> OtherFiles { get; set; } = new();
    
    public List<string> AllAttachments { get; set; } = new();
    public List<FileAttachmentInfo> AllAttachmentDetails { get; set; } = new();
    public List<string> UnmatchedFiles { get; set; } = new();
    
    public int TotalFileCount => AllAttachments.Count;
    public long TotalFileSize => AllAttachmentDetails.Sum(f => f.FileSize);
    
    public string TotalFileSizeFormatted
    {
        get
        {
            var size = TotalFileSize;
            if (size < 1024)
                return $"{size} B";
            else if (size < 1024 * 1024)
                return $"{size / 1024.0:F2} KB";
            else
                return $"{size / (1024.0 * 1024.0):F2} MB";
        }
    }
}
