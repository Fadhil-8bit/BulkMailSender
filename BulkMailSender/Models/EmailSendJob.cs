namespace BulkMailSender.Models;

public class EmailSendJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Email data
    public List<DebtorRecipient> Recipients { get; set; } = new();
    public UploadResult? UploadResult { get; set; }
    public SavedTemplate? Template { get; set; }
    public SmtpSettings? SmtpSettings { get; set; }

    // Progress tracking
    public int TotalEmails { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public string CurrentDebtor { get; set; } = string.Empty;

    // Results
    public SendSummary Results { get; set; } = new();

    // Error tracking
    public string? ErrorMessage { get; set; }
}

public enum JobStatus
{
    Queued,      // Waiting to be processed
    Running,     // Currently sending emails
    Completed,   // All emails processed
    Failed,      // Critical error occurred
    Cancelled    // User cancelled the job
}

public class SendSummary
{
    public List<PerDebtorResult> Sent { get; set; } = new();
    public List<PerDebtorResult> Failed { get; set; } = new();
    public List<PerDebtorResult> Skipped { get; set; } = new();
}

public class PerDebtorResult
{
    public string DebtorCode { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Template Type enum and SavedTemplate model
public enum TemplateType
{
    SoaInv,
    Overdue
}

public class SavedTemplate
{
    public TemplateType TemplateType { get; set; }
    public string Period { get; set; } = string.Empty;
    public string DebtorCode { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
