using Data.Enum;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessObjectLayer.BackgroundJobs
{
    public class PaymentCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _timeoutThreshold = TimeSpan.FromMinutes(15);

        public PaymentCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ PaymentCleanupService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndFailTimedOutPaymentsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in PaymentCleanupService");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("⏹️ PaymentCleanupService stopped.");
        }

        private async Task CheckAndFailTimedOutPaymentsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var paymentRepository = uow.GetRepository<IPaymentRepository>();

            // Calculate cutoff time (15 minutes ago)
            var cutoff = DateTime.UtcNow.AddMinutes(-15);

            _logger.LogInformation($"🔍 Checking for timed-out payments (before {cutoff:yyyy-MM-dd HH:mm:ss} UTC)");

            // Get all pending payments that have timed out
            var timedOutPayments = await paymentRepository.GetPendingBeforeAsync(cutoff);

            if (timedOutPayments.Count == 0)
            {
                _logger.LogInformation("✅ No timed-out payments found.");
                return;
            }

            _logger.LogInformation($"⚠️ Found {timedOutPayments.Count} timed-out payment(s).");

            await uow.BeginTransactionAsync();
            try
            {
                // Update each payment to Failed status
                int updatedCount = 0;
                foreach (var payment in timedOutPayments)
                {
                    try
                    {
                        payment.PaymentStatus = PaymentStatusEnum.Failed;
                        await paymentRepository.UpdateAsync(payment);
                        updatedCount++;

                        _logger.LogInformation(
                            $"❌ Payment {payment.PaymentId} (CompanyId: {payment.CompanyId}) marked as Failed due to timeout.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Failed to update payment {payment.PaymentId}");
                    }
                }

                // Save all changes to the database
                await uow.CommitTransactionAsync();
                _logger.LogInformation($"✅ Successfully updated {updatedCount}/{timedOutPayments.Count} timed-out payment(s) to Failed status.");
            }
            catch (Exception ex)
            {
                await uow.RollbackTransactionAsync();
                _logger.LogError(ex, $"❌ Failed to save changes to database. Transaction rolled back.");
            }
        }
    }
}


