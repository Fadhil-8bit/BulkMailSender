using BulkMailSender.Models;
using System.Net;
using System.Net.Mail;

namespace BulkMailSender.Services;

public class BackgroundEmailSendService : BackgroundService
{
    private readonly EmailSendQueueService _queueService;
    private readonly ILogger<BackgroundEmailSendService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundEmailSendService(
        EmailSendQueueService queueService,
        ILogger<BackgroundEmailSendService> logger,
        IServiceProvider serviceProvider)
    {
        _queueService = queueService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundEmailSendService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queueService.TryDequeueJob(out var job) && job != null)
                {
                    _logger.LogInformation("Processing job {JobId}", job.JobId);
                    await ProcessJobAsync(job, stoppingToken);
                }
                else
                {
                    // No jobs in queue, wait a bit
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background email send service");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("BackgroundEmailSendService stopped");
    }

    private async Task ProcessJobAsync(EmailSendJob job, CancellationToken cancellationToken)
    {
        try
        {
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            _queueService.UpdateJob(job);

            var grouped = job.Recipients.GroupBy(r => r.DebtorCode).OrderBy(g => g.Key).ToList();

            foreach (var group in grouped)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested || job.Status == JobStatus.Cancelled)
                {
                    _logger.LogInformation("Job {JobId} cancelled", job.JobId);
                    job.Status = JobStatus.Cancelled;
                    job.CompletedAt = DateTime.UtcNow;
                    _queueService.UpdateJob(job);
                    return;
                }

                var debtorCode = group.Key;
                var recipientList = group.ToList();

                job.CurrentDebtor = debtorCode;
                _queueService.UpdateJob(job);

                // Find attachments for debtor
                var debtorAttach = job.UploadResult?.DebtorAttachments.FirstOrDefault(
                    d => string.Equals(d.DebtorCode, debtorCode, StringComparison.OrdinalIgnoreCase));

                // Determine attachments based on template type
                var attachmentsToAdd = new List<string>();
                if (job.Template != null && debtorAttach != null)
                {
                    if (job.Template.TemplateType == TemplateType.SoaInv)
                    {
                        attachmentsToAdd.AddRange(debtorAttach.InvoiceFiles.Select(f => f.FilePath));
                        attachmentsToAdd.AddRange(debtorAttach.StatementFiles.Select(f => f.FilePath));
                    }
                    else
                    {
                        attachmentsToAdd.AddRange(debtorAttach.OverdueFiles.Select(f => f.FilePath));
                    }
                }

                if (!attachmentsToAdd.Any())
                {
                    job.Results.Skipped.Add(new PerDebtorResult
                    {
                        DebtorCode = debtorCode,
                        Reason = "No attachments found for this debtor and template type."
                    });
                    job.SkippedCount++;
                    _queueService.UpdateJob(job);
                    continue;
                }

                // Send email with retry logic
                bool sent = await SendEmailWithRetryAsync(job, debtorCode, recipientList, attachmentsToAdd, cancellationToken);

                if (sent)
                {
                    job.Results.Sent.Add(new PerDebtorResult
                    {
                        DebtorCode = debtorCode,
                        To = recipientList.Where(x => x.Label == EmailLabel.To).Select(x => x.Email).ToList(),
                        Message = "Sent"
                    });
                    job.SentCount++;
                }
                else
                {
                    job.FailedCount++;
                }

                _queueService.UpdateJob(job);
            }

            // Job completed successfully
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.CurrentDebtor = string.Empty;
            _queueService.UpdateJob(job);

            _logger.LogInformation("Job {JobId} completed. Sent: {Sent}, Failed: {Failed}, Skipped: {Skipped}",
                job.JobId, job.SentCount, job.FailedCount, job.SkippedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", job.JobId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _queueService.UpdateJob(job);
        }
    }

    private async Task<bool> SendEmailWithRetryAsync(
        EmailSendJob job,
        string debtorCode,
        List<DebtorRecipient> recipientList,
        List<string> attachments,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount < maxRetries)
        {
            try
            {
                using var msg = new MailMessage();
                msg.From = new MailAddress(
                    job.SmtpSettings?.FromEmail ?? job.SmtpSettings?.Username ?? "noreply@example.com",
                    job.SmtpSettings?.FromName ?? "Bulk Mail Sender");

                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.To)) msg.To.Add(r.Email);
                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.Cc)) msg.CC.Add(r.Email);
                foreach (var r in recipientList.Where(x => x.Label == EmailLabel.Bcc)) msg.Bcc.Add(r.Email);

                // Add global CC recipients
                if (!string.IsNullOrWhiteSpace(job.SmtpSettings?.GlobalCc))
                {
                    var globalCcEmails = job.SmtpSettings.GlobalCc
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e));

                    foreach (var email in globalCcEmails)
                    {
                        try
                        {
                            // Validate email before adding
                            var addr = new MailAddress(email);
                            // Avoid duplicate CC
                            if (!msg.CC.Any(cc => cc.Address.Equals(email, StringComparison.OrdinalIgnoreCase)))
                            {
                                msg.CC.Add(addr);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Invalid global CC email '{Email}': {Error}", email, ex.Message);
                        }
                    }
                }

                // Replace placeholders
                var org = recipientList.FirstOrDefault()?.OrganizationName ?? "{organization name}";
                var notes = recipientList.FirstOrDefault()?.Notes ?? "{notes}";
                var debtorPlaceholder = debtorCode;

                var subject = job.Template?.Subject ?? string.Empty;
                var body = job.Template?.Body ?? string.Empty;
                subject = subject.Replace("{organization name}", org)
                                .Replace("{notes}", notes)
                                .Replace("{debtor code}", debtorPlaceholder);
                body = body.Replace("{organization name}", org)
                          .Replace("{notes}", notes)
                          .Replace("{debtor code}", debtorPlaceholder);

                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = false;

                // Attach files
                foreach (var path in attachments.Distinct())
                {
                    if (File.Exists(path))
                    {
                        msg.Attachments.Add(new Attachment(path));
                    }
                }

                using var client = new SmtpClient(job.SmtpSettings?.Host ?? "localhost", job.SmtpSettings?.Port ?? 587)
                {
                    EnableSsl = job.SmtpSettings?.EnableSsl ?? true,
                    Timeout = (job.SmtpSettings?.TimeoutSeconds ?? 30) * 1000,
                    Credentials = string.IsNullOrWhiteSpace(job.SmtpSettings?.Username)
                        ? null
                        : new NetworkCredential(job.SmtpSettings.Username, job.SmtpSettings.Password)
                };

                await client.SendMailAsync(msg, cancellationToken);

                _logger.LogInformation("Email sent to debtor {Debtor}", debtorCode);
                return true;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                if (retryCount < maxRetries)
                {
                    var delay = (int)Math.Pow(2, retryCount) * 1000; // Exponential backoff: 2s, 4s, 8s
                    _logger.LogWarning("Failed to send email to {Debtor}. Retry {Retry}/{Max} in {Delay}ms. Error: {Error}",
                        debtorCode, retryCount, maxRetries, delay, ex.Message);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    _logger.LogError(ex, "Failed to send email to {Debtor} after {Max} retries", debtorCode, maxRetries);
                    job.Results.Failed.Add(new PerDebtorResult
                    {
                        DebtorCode = debtorCode,
                        Reason = $"Failed after {maxRetries} retries: {ex.Message}",
                        RetryCount = retryCount
                    });
                }
            }
        }

        return false;
    }
}
