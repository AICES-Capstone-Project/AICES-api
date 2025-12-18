using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services.UsageLimits
{
    public class ResumeLimitService : IResumeLimitService
    {
        private readonly IUnitOfWork _uow;

        public ResumeLimitService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        /// <summary>
        /// Check resume limit (read-only, fast check before expensive operations)
        /// Used outside transaction for early validation
        /// </summary>
        public async Task<ServiceResponse> CheckResumeLimitAsync(int companyId)
        {
            try
            {
                var (resumeLimit, hoursLimit, periodStartDate, periodEndDate, companySubscriptionId) 
                    = await GetSubscriptionLimitInfoAsync(companyId);

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed (no limit)."
                    };
                }

                var usageCounterRepo = _uow.GetRepository<IUsageCounterRepository>();
                
                // Fast read-only check
                var canUse = await usageCounterRepo.CanUseAsync(
                    companyId,
                    UsageTypeEnum.Resume,
                    periodStartDate,
                    periodEndDate);

                if (!canUse)
                {
                    var currentUsage = await usageCounterRepo.GetCurrentUsageAsync(
                        companyId,
                        UsageTypeEnum.Resume,
                        periodStartDate,
                        periodEndDate);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Resume upload limit reached. You have used {currentUsage}/{resumeLimit} resumes in the last {hoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = currentUsage,
                            Limit = resumeLimit,
                            HoursLimit = hoursLimit,
                            Remaining = 0
                        }
                    };
                }

                var usage = await usageCounterRepo.GetCurrentUsageAsync(
                    companyId,
                    UsageTypeEnum.Resume,
                    periodStartDate,
                    periodEndDate);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume limit check passed.",
                    Data = new
                    {
                        CurrentCount = usage,
                        Limit = resumeLimit,
                        HoursLimit = hoursLimit,
                        Remaining = resumeLimit - usage
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking resume limit: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking resume limit: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Check and increment resume limit atomically within transaction
        /// MUST be called within a transaction (BeginTransactionAsync)
        /// This is the critical method that prevents race conditions
        /// </summary>
        public async Task<ServiceResponse> CheckResumeLimitInTransactionAsync(int companyId)
        {
            try
            {
                var (resumeLimit, hoursLimit, periodStartDate, periodEndDate, companySubscriptionId) 
                    = await GetSubscriptionLimitInfoAsync(companyId);

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed (no limit)."
                    };
                }

                var usageCounterRepo = _uow.GetRepository<IUsageCounterRepository>();
                
                // Step 1: Get or create counter (ensures counter exists)
                var counter = await usageCounterRepo.GetOrCreateCounterAsync(
                    companyId,
                    UsageTypeEnum.Resume,
                    companySubscriptionId,
                    periodStartDate,
                    periodEndDate,
                    resumeLimit);
                
                await _uow.SaveChangesAsync(); // Save counter if newly created

                // Step 2: ATOMIC check and increment with SELECT FOR UPDATE
                // This is the key operation that prevents race conditions
                var success = await usageCounterRepo.CheckAndIncrementIfAllowedAsync(
                    companyId,
                    UsageTypeEnum.Resume,
                    periodStartDate,
                    periodEndDate);

                if (!success)
                {
                    // Quota exceeded - reload counter to get current values
                    var currentCounter = await usageCounterRepo.GetCounterForReadAsync(
                        companyId,
                        UsageTypeEnum.Resume,
                        periodStartDate,
                        periodEndDate);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Resume upload limit reached. You have used {currentCounter?.Used ?? 0}/{currentCounter?.Limit ?? 0} resumes in the last {hoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = currentCounter?.Used ?? 0,
                            Limit = currentCounter?.Limit ?? 0,
                            HoursLimit = hoursLimit,
                            Remaining = 0
                        }
                    };
                }

                // Success - counter has been incremented atomically
                var updatedCounter = await usageCounterRepo.GetCounterForReadAsync(
                    companyId,
                    UsageTypeEnum.Resume,
                    periodStartDate,
                    periodEndDate);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume limit check passed and quota reserved.",
                    Data = new
                    {
                        CurrentCount = updatedCounter?.Used ?? 0,
                        Limit = updatedCounter?.Limit ?? 0,
                        HoursLimit = hoursLimit,
                        Remaining = (updatedCounter?.Limit ?? 0) - (updatedCounter?.Used ?? 0)
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking resume limit in transaction: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking resume limit: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get subscription limit info and calculate period
        /// Returns: (resumeLimit, hoursLimit, periodStartDate, periodEndDate, companySubscriptionId)
        /// </summary>
        private async Task<(int resumeLimit, int hoursLimit, DateTime periodStartDate, DateTime periodEndDate, int? companySubscriptionId)> 
            GetSubscriptionLimitInfoAsync(int companyId)
        {
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);

            int resumeLimit;
            int hoursLimit;
            DateTime periodStartDate;
            DateTime periodEndDate;
            int? companySubscriptionId;

            if (companySubscription == null)
            {
                // Free plan: fixed window based on hours
                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
                
                if (freeSubscription == null)
                {
                    throw new InvalidOperationException("No active subscription found and Free subscription not configured.");
                }

                resumeLimit = freeSubscription.ResumeLimit;
                hoursLimit = freeSubscription.HoursLimit;
                companySubscriptionId = null;

                // ✅ FIXED: Round period to start of hour/day to keep it consistent
                var now = DateTime.UtcNow;
                
                if (hoursLimit >= 24)
                {
                    // Daily or longer: round to start of day
                    periodStartDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    periodEndDate = periodStartDate.AddHours(hoursLimit);
                }
                else
                {
                    // Hourly: round to start of current hour
                    periodStartDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                    periodEndDate = periodStartDate.AddHours(hoursLimit);
                }
            }
            else
            {
                // Paid plan: fixed period from subscription start
                resumeLimit = companySubscription.Subscription?.ResumeLimit ?? 0;
                hoursLimit = companySubscription.Subscription?.HoursLimit ?? 0;
                companySubscriptionId = companySubscription.ComSubId;

                // ✅ FIXED: Round subscription start to start of hour for consistency
                var subStart = companySubscription.StartDate;
                periodStartDate = new DateTime(subStart.Year, subStart.Month, subStart.Day, subStart.Hour, 0, 0, DateTimeKind.Utc);
                periodEndDate = periodStartDate.AddHours(hoursLimit);
            }

            return (resumeLimit, hoursLimit, periodStartDate, periodEndDate, companySubscriptionId);
        }
    }
}
