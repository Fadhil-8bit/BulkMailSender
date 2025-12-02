namespace BulkMailSender.Models;

public class UploadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<DebtorAttachment> DebtorAttachments { get; set; } = new();
    public int TotalFiles { get; set; }
    public int CategorizedFiles { get; set; }
    public int UncategorizedFiles { get; set; }
    public List<string> Errors { get; set; } = new();
}
