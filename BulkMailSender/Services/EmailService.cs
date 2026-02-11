using System.Net;
using System.Net.Mail;
using BulkMailSender.Models;

namespace BulkMailSender.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(EmailSettings settings)
    {
        const int maxRetries = 3;
        int delay = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var client = new SmtpClient(settings.Host, settings.Port)
                {
                    Credentials = string.IsNullOrWhiteSpace(settings.Username)
                        ? null
                        : new NetworkCredential(settings.Username, settings.Password),
                    EnableSsl = settings.EnableSsl,
                    Timeout = settings.TimeoutSeconds * 1000
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(settings.FromEmail, settings.FromName),
                    Subject = "ATP Bulk Mailer - Test Connection",
                    Body = "This is a test email from ATP Bulk Mail Sender.\n\nIf you receive this, your SMTP settings are correct.",
                    IsBodyHtml = false
                };
                message.To.Add(new MailAddress(settings.FromEmail));
                
                await client.SendMailAsync(message);
                
                _logger.LogInformation("SMTP Test Connection Successful: {Host}:{Port}", settings.Host, settings.Port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMTP Test Connection Attempt {Attempt} failed.", i + 1);
                
                if (i == maxRetries - 1)
                {
                    // Propagate the last exception so the caller knows why it failed
                    throw; 
                }

                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }
        
        return false;
    }
}
