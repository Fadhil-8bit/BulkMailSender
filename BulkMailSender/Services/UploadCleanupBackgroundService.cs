using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BulkMailSender.Services
{
    public class UploadCleanupBackgroundService : BackgroundService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UploadCleanupBackgroundService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
        private readonly TimeSpan _folderAgeLimit = TimeSpan.FromHours(4);

        public UploadCleanupBackgroundService(
            IWebHostEnvironment environment,
            ILogger<UploadCleanupBackgroundService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Upload Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Run cleanup immediately on start, then wait
                // Or maybe wait first? Usually good to clean up on start in case it was down.
                // But let's follow standard pattern: loop -> do work -> delay.
                
                try
                {
                    CleanupOldUploads();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up old uploads.");
                }

                // Wait for the next interval
                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation("Upload Cleanup Service is stopping.");
        }

        private void CleanupOldUploads()
        {
            if (string.IsNullOrEmpty(_environment.WebRootPath))
            {
                 _logger.LogWarning("WebRootPath is null or empty, skipping cleanup.");
                 return;
            }

            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");

            if (!Directory.Exists(uploadsPath))
            {
                return;
            }

            var directories = Directory.GetDirectories(uploadsPath);
            var now = DateTime.UtcNow;
            int deletedCount = 0;

            foreach (var dir in directories)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    // Check if folder is older than limit
                    if (now - dirInfo.CreationTimeUtc > _folderAgeLimit)
                    {
                        dirInfo.Delete(true); // Recursive delete
                        deletedCount++;
                        _logger.LogInformation($"Deleted old upload folder: {dirInfo.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete folder {dir}.");
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation($"Cleanup complete. Deleted {deletedCount} old folders.");
            }
        }
    }
}
