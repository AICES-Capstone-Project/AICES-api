using Data.Entities;
using Data.Enum;
using System;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IUsageCounterRepository
    {
        /// <summary>
        /// Get or create counter for a company/usageType/period.
        /// Creates counter if not exists, updates limit if changed.
        /// </summary>
        Task<UsageCounter?> GetOrCreateCounterAsync(
            int companyId,
            UsageTypeEnum usageType,
            int? companySubscriptionId,
            DateTime periodStartDate,
            DateTime periodEndDate,
            int limit);

        /// <summary>
        /// Get counter for update (no lock, tracking enabled).
        /// Used to reload counter after atomic operations.
        /// </summary>
        Task<UsageCounter?> GetCounterForReadAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate);

        /// <summary>
        /// Atomic check and increment counter in one query.
        /// Returns true if increment succeeded (quota available), false if limit reached.
        /// MUST be called within a transaction for SELECT FOR UPDATE to work.
        /// </summary>
        Task<bool> CheckAndIncrementIfAllowedAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate);

        /// <summary>
        /// Get current usage count for a period.
        /// Fast read-only query for dashboard/reporting.
        /// </summary>
        Task<int> GetCurrentUsageAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate);

        /// <summary>
        /// Check if usage is allowed without incrementing.
        /// Used for early validation before expensive operations.
        /// </summary>
        Task<bool> CanUseAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate);

        /// <summary>
        /// Reset all usage counters for a company by soft deleting them (IsActive = false).
        /// This should be called when subscription changes (upgrade or cancel).
        /// </summary>
        Task ResetAllUsageCountersAsync(int companyId);

        /// <summary>
        /// Archive current usage counters and reset usage to 0 for active period.
        /// This preserves historical data while resetting the usage count.
        /// Used when subscription changes (upgrade/downgrade/cancel).
        /// </summary>
        Task ArchiveAndResetUsageCountersAsync(int companyId);
    }
}
