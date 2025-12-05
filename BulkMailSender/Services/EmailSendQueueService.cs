using BulkMailSender.Models;
using System.Collections.Concurrent;

namespace BulkMailSender.Services;

public class EmailSendQueueService
{
    private readonly ConcurrentQueue<EmailSendJob> _jobQueue = new();
    private readonly ConcurrentDictionary<string, EmailSendJob> _jobs = new();
    private readonly ILogger<EmailSendQueueService> _logger;

    public EmailSendQueueService(ILogger<EmailSendQueueService> logger)
    {
        _logger = logger;
    }

    public string EnqueueJob(EmailSendJob job)
    {
        job.Status = JobStatus.Queued;
        job.CreatedAt = DateTime.UtcNow;

        _jobs.TryAdd(job.JobId, job);
        _jobQueue.Enqueue(job);

        _logger.LogInformation("Job {JobId} enqueued with {Count} emails", job.JobId, job.TotalEmails);
        return job.JobId;
    }

    public bool TryDequeueJob(out EmailSendJob? job)
    {
        if (_jobQueue.TryDequeue(out var dequeuedJob))
        {
            job = dequeuedJob;
            return true;
        }

        job = null;
        return false;
    }

    public EmailSendJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public void UpdateJob(EmailSendJob job)
    {
        _jobs.AddOrUpdate(job.JobId, job, (key, oldValue) => job);
    }

    public bool CancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Queued || job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Cancelled;
                _logger.LogInformation("Job {JobId} cancelled", jobId);
                return true;
            }
        }
        return false;
    }

    public int GetQueuedJobCount()
    {
        return _jobQueue.Count;
    }

    public IEnumerable<EmailSendJob> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.CreatedAt);
    }
}
