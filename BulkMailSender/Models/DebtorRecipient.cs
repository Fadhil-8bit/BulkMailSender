namespace BulkMailSender.Models;

public enum EmailLabel
{
    To,
    Cc,
    Bcc
}

public class DebtorRecipient
{
    public string DebtorCode { get; set; } = string.Empty; // first name - Debtor Code
    public string? OrganizationName { get; set; } // organization name
    public string? Notes { get; set; } // e.g., 90Days
    public string Email { get; set; } = string.Empty; // company email
    public EmailLabel Label { get; set; } // Work(TO), View(CC), Private(BCC)
}
