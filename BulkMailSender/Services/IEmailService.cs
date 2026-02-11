using BulkMailSender.Models;

namespace BulkMailSender.Services;

public interface IEmailService
{
    /// <summary>
    /// Tests the SMTP connection by sending a test email to the sender address.
    /// Includes retry logic for reliability.
    /// </summary>
    /// <param name="settings">The SMTP configuration to test.</param>
    /// <returns>True if successful, otherwise throws specific exceptions.</returns>
    Task<bool> TestConnectionAsync(EmailSettings settings);
}
