using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
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
                using var message = new MimeMessage();
                message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
                message.To.Add(new MailboxAddress("", settings.FromEmail));
                message.Subject = "ATP Bulk Mailer - Test Connection";
                message.Body = new TextPart("plain")
                {
                    Text = "This is a test email from ATP Bulk Mail Sender.\n\nIf you receive this, your SMTP settings are correct."
                };

                using var client = new SmtpClient();
                client.Timeout = settings.TimeoutSeconds * 1000;

                await client.ConnectAsync(settings.Host, settings.Port, settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    await client.AuthenticateAsync(settings.Username, settings.Password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

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
