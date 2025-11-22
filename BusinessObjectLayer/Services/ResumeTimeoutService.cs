using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessObjectLayer.Services
{
    public class ResumeTimeoutService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ResumeTimeoutService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _timeoutThreshold = TimeSpan.FromMinutes(2);

        public ResumeTimeoutService(
            IServiceScopeFactory scopeFactory,
            ILogger<ResumeTimeoutService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ ResumeTimeoutService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndFailTimedOutResumesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in ResumeTimeoutService");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("⏹️ ResumeTimeoutService stopped.");
        }

        private async Task CheckAndFailTimedOutResumesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var parsedResumeRepository = scope.ServiceProvider.GetRequiredService<IParsedResumeRepository>();

            // Calculate cutoff time (5 minutes ago)
            var cutoff = DateTime.UtcNow.AddMinutes(-2);

            _logger.LogInformation($"🔍 Checking for timed-out resumes (before {cutoff:yyyy-MM-dd HH:mm:ss} UTC)");

            // Get all pending resumes that have timed out
            var timedOutResumes = await parsedResumeRepository.GetPendingBeforeAsync(cutoff);

            if (timedOutResumes.Count == 0)
            {
                _logger.LogInformation("✅ No timed-out resumes found.");
                return;
            }

            _logger.LogInformation($"⚠️ Found {timedOutResumes.Count} timed-out resume(s).");

            // Update each resume to Failed status
            int updatedCount = 0;
            foreach (var resume in timedOutResumes)
            {
                try
                {
                    resume.ResumeStatus = ResumeStatusEnum.Failed;
                    await parsedResumeRepository.UpdateAsync(resume);
                    updatedCount++;

                    _logger.LogInformation(
                        $"❌ Resume {resume.ResumeId} (QueueJobId: {resume.QueueJobId}) marked as Failed due to timeout.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to update resume {resume.ResumeId}");
                }
            }

            _logger.LogInformation($"✅ Successfully updated {updatedCount}/{timedOutResumes.Count} timed-out resume(s) to Failed status.");
        }
    }
}

