using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services.UsageLimits
{
    public class ComparisonLimitService : IComparisonLimitService
    {
        private readonly IUnitOfWork _uow;

        public ComparisonLimitService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        /// <summary>
        /// Check comparison limit (read-only, fast check before expensive operations)
        /// Used outside transaction for early validation
        /// </summary>
        public async Task<ServiceResponse> CheckComparisonLimitAsync(int companyId)
        {
            try
            {
                var (compareLimit, compareHoursLimit, periodStartDate, periodEndDate, companySubscriptionId) 
                    = await GetSubscriptionLimitInfoAsync(companyId);

                // ✅ Correct logic for nullable compareLimit:
                // - null → unlimited (no tracking)
                // - 0 → NO comparisons allowed (reject)
                // - > 0 → enforce limit with usage counter
                
                if (!compareLimit.HasValue)
                {
                    // No limit set (null), allow unlimited comparisons
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Comparison limit check passed (unlimited)."
                    };
                }

                if (compareLimit.Value == 0)
                {
                    // Limit is 0, no comparisons allowed (Free plan)
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Comparison feature is not available in your current plan. Please upgrade to access candidate comparison.",
                        Data = new
                        {
                            CurrentCount = 0,
                            Limit = 0,
                            HoursLimit = compareHoursLimit,
                            Remaining = 0
                        }
                    };
                }

                // compareLimit > 0, enforce with usage counter
                var usageCounterRepo = _uow.GetRepository<IUsageCounterRepository>();
                
                // Fast read-only check
                var canUse = await usageCounterRepo.CanUseAsync(
                    companyId,
                    UsageTypeEnum.Comparison,
                    periodStartDate,
                    periodEndDate);

                if (!canUse)
                {
                    var currentUsage = await usageCounterRepo.GetCurrentUsageAsync(
                        companyId,
                        UsageTypeEnum.Comparison,
                        periodStartDate,
                        periodEndDate);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Comparison limit reached. You have used {currentUsage}/{compareLimit.Value} comparisons in the last {compareHoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = currentUsage,
                            Limit = compareLimit.Value,
                            HoursLimit = compareHoursLimit,
                            Remaining = 0
                        }
                    };
                }

                var usage = await usageCounterRepo.GetCurrentUsageAsync(
                    companyId,
                    UsageTypeEnum.Comparison,
                    periodStartDate,
                    periodEndDate);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Comparison limit check passed.",
                    Data = new
                    {
                        CurrentCount = usage,
                        Limit = compareLimit.Value,
                        HoursLimit = compareHoursLimit,
                        Remaining = compareLimit.Value - usage
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking comparison limit: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking comparison limit: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Check and increment comparison limit atomically within transaction
        /// MUST be called within a transaction (BeginTransactionAsync)
        /// This is the critical method that prevents race conditions
        /// </summary>
        public async Task<ServiceResponse> CheckComparisonLimitInTransactionAsync(int companyId)
        {
            try
            {
                var (compareLimit, compareHoursLimit, periodStartDate, periodEndDate, companySubscriptionId) 
                    = await GetSubscriptionLimitInfoAsync(companyId);

                // ✅ Correct logic for nullable compareLimit:
                // - null → unlimited (no tracking)
                // - 0 → NO comparisons allowed (reject)
                // - > 0 → enforce limit with usage counter
                
                if (!compareLimit.HasValue)
                {
                    // No limit set (null), allow unlimited comparisons
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Comparison limit check passed (unlimited)."
                    };
                }

                if (compareLimit.Value == 0)
                {
                    // Limit is 0, no comparisons allowed (Free plan)
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Comparison feature is not available in your current plan. Please upgrade to access candidate comparison.",
                        Data = new
                        {
                            CurrentCount = 0,
                            Limit = 0,
                            HoursLimit = compareHoursLimit,
                            Remaining = 0
                        }
                    };
                }

                // compareLimit > 0, enforce with usage counter
                var usageCounterRepo = _uow.GetRepository<IUsageCounterRepository>();
                
                // Step 1: Get or create counter (ensures counter exists)
                var counter = await usageCounterRepo.GetOrCreateCounterAsync(
                    companyId,
                    UsageTypeEnum.Comparison,
                    companySubscriptionId,
                    periodStartDate,
                    periodEndDate,
                    compareLimit.Value);
                
                await _uow.SaveChangesAsync(); // Save counter if newly created

                // Step 2: ATOMIC check and increment with SELECT FOR UPDATE
                // This is the key operation that prevents race conditions
                var success = await usageCounterRepo.CheckAndIncrementIfAllowedAsync(
                    companyId,
                    UsageTypeEnum.Comparison,
                    periodStartDate,
                    periodEndDate);

                if (!success)
                {
                    // Quota exceeded - reload counter to get current values
                    var currentCounter = await usageCounterRepo.GetCounterForReadAsync(
                        companyId,
                        UsageTypeEnum.Comparison,
                        periodStartDate,
                        periodEndDate);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Comparison limit reached. You have used {currentCounter?.Used ?? 0}/{compareLimit.Value} comparisons in the last {compareHoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = currentCounter?.Used ?? 0,
                            Limit = compareLimit.Value,
                            HoursLimit = compareHoursLimit,
                            Remaining = 0
                        }
                    };
                }

                // Success - counter has been incremented atomically
                var updatedCounter = await usageCounterRepo.GetCounterForReadAsync(
                    companyId,
                    UsageTypeEnum.Comparison,
                    periodStartDate,
                    periodEndDate);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Comparison limit check passed and quota reserved.",
                    Data = new
                    {
                        CurrentCount = updatedCounter?.Used ?? 0,
                        Limit = compareLimit.Value,
                        HoursLimit = compareHoursLimit,
                        Remaining = compareLimit.Value - (updatedCounter?.Used ?? 0)
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking comparison limit in transaction: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking comparison limit: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get subscription limit info and calculate period for comparison
        /// Returns: (compareLimit, compareHoursLimit, periodStartDate, periodEndDate, companySubscriptionId)
        /// </summary>
        private async Task<(int? compareLimit, int compareHoursLimit, DateTime periodStartDate, DateTime periodEndDate, int? companySubscriptionId)> 
            GetSubscriptionLimitInfoAsync(int companyId)
        {
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);

            int? compareLimit;  // ✅ Nullable to distinguish between "no limit" (null), "no access" (0), and "limited" (> 0)
            int compareHoursLimit;
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

                // ✅ Keep CompareLimit as nullable from Free subscription
                // null = unlimited, 0 = no access, > 0 = limited
                compareLimit = freeSubscription.CompareLimit;  // Don't coalesce to 0!
                compareHoursLimit = freeSubscription.CompareHoursLimit ?? 1;  // Default 24h if not set
                companySubscriptionId = null;

                // ✅ FIXED: Round period to start of hour/day to keep it consistent
                var now = DateTime.UtcNow;
                
                if (compareHoursLimit >= 24)
                {
                    // Daily or longer: round to start of day
                    periodStartDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    periodEndDate = periodStartDate.AddHours(compareHoursLimit);
                }
                else if (compareHoursLimit > 0)
                {
                    // Hourly: round to start of current hour
                    periodStartDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                    periodEndDate = periodStartDate.AddHours(compareHoursLimit);
                }
                else
                {
                    // No time limit configured, use a default window (e.g., 24 hours)
                    periodStartDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    periodEndDate = periodStartDate.AddHours(24);
                }
            }
            else
            {
                // Paid plan: fixed period from subscription start
                // ✅ Keep CompareLimit as nullable from subscription
                compareLimit = companySubscription.Subscription?.CompareLimit;  // Don't coalesce to 0!
                compareHoursLimit = companySubscription.Subscription?.CompareHoursLimit ?? 24;  // Default 24h if not set
                companySubscriptionId = companySubscription.ComSubId;

                // ✅ FIXED: Round subscription start to start of hour for consistency
                var subStart = companySubscription.StartDate;
                periodStartDate = new DateTime(subStart.Year, subStart.Month, subStart.Day, subStart.Hour, 0, 0, DateTimeKind.Utc);
                
                if (compareHoursLimit > 0)
                {
                    periodEndDate = periodStartDate.AddHours(compareHoursLimit);
                }
                else
                {
                    // No time limit configured, use subscription duration
                    periodEndDate = periodStartDate.AddHours(24);
                }
            }

            return (compareLimit, compareHoursLimit, periodStartDate, periodEndDate, companySubscriptionId);
        }
    }
}

