using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class UsageCounterRepository : IUsageCounterRepository
    {
        private readonly AICESDbContext _context;

        public UsageCounterRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<UsageCounter?> GetOrCreateCounterAsync(
            int companyId,
            UsageTypeEnum usageType,
            int? companySubscriptionId,
            DateTime periodStartDate,
            DateTime periodEndDate,
            int limit)
        {
            var counter = await _context.UsageCounters
                .FirstOrDefaultAsync(c =>
                    c.CompanyId == companyId
                    && c.UsageType == usageType
                    && c.PeriodStartDate == periodStartDate
                    && c.PeriodEndDate == periodEndDate
                    && c.IsActive == true
                    && c.Status == UsageCounterStatusEnum.Active);

            if (counter == null)
            {
                counter = new UsageCounter
                {
                    CompanyId = companyId,
                    CompanySubscriptionId = companySubscriptionId,
                    UsageType = usageType,
                    PeriodStartDate = periodStartDate,
                    PeriodEndDate = periodEndDate,
                    Used = 0,
                    Limit = limit,
                    Status = UsageCounterStatusEnum.Active,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.UsageCounters.AddAsync(counter);
            }
            else
            {
                // ✅ Update limit and subscription ID if changed (e.g., subscription upgraded/downgraded)
                bool updated = false;
                
                if (counter.Limit != limit)
                {
                    counter.Limit = limit;
                    updated = true;
                }
                
                // Update CompanySubscriptionId to reflect current subscription
                if (counter.CompanySubscriptionId != companySubscriptionId)
                {
                    counter.CompanySubscriptionId = companySubscriptionId;
                    updated = true;
                }
                
                if (updated)
                {
                    counter.UpdatedAt = DateTime.UtcNow;
                }
            }

            return counter;
        }

        public async Task<UsageCounter?> GetCounterForReadAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate)
        {
            // No AsNoTracking() to allow EF to track changes
            return await _context.UsageCounters
                .FirstOrDefaultAsync(c =>
                    c.CompanyId == companyId
                    && c.UsageType == usageType
                    && c.PeriodStartDate == periodStartDate
                    && c.PeriodEndDate == periodEndDate
                    && c.IsActive == true
                    && c.Status == UsageCounterStatusEnum.Active);
        }

        public async Task<bool> CheckAndIncrementIfAllowedAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate)
        {
            // Atomic check and increment trong 1 query với SELECT FOR UPDATE
            // CRITICAL: PHẢI nằm trong transaction để FOR UPDATE hoạt động
            var result = await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE ""UsageCounters""
                SET ""Used"" = ""Used"" + 1,
                    ""UpdatedAt"" = CURRENT_TIMESTAMP
                WHERE ""UsageCounterId"" = (
                    SELECT ""UsageCounterId"" FROM ""UsageCounters""
                    WHERE ""CompanyId"" = {0}
                        AND ""UsageType"" = {1}
                        AND ""PeriodStartDate"" = {2}
                        AND ""PeriodEndDate"" = {3}
                        AND ""IsActive"" = true
                        AND ""Status"" = 'Active'
                        AND ""Used"" < ""Limit""
                    FOR UPDATE
                    LIMIT 1
                )
            ", companyId, usageType.ToString(), periodStartDate, periodEndDate);

            // result > 0 = update thành công (có quota)
            // result = 0 = không update (hết quota hoặc không tìm thấy counter)
            return result > 0;
        }

        public async Task<int> GetCurrentUsageAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate)
        {
            var counter = await _context.UsageCounters
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.CompanyId == companyId
                    && c.UsageType == usageType
                    && c.PeriodStartDate == periodStartDate
                    && c.PeriodEndDate == periodEndDate
                    && c.IsActive == true
                    && c.Status == UsageCounterStatusEnum.Active);

            return counter?.Used ?? 0;
        }

        public async Task<bool> CanUseAsync(
            int companyId,
            UsageTypeEnum usageType,
            DateTime periodStartDate,
            DateTime periodEndDate)
        {
            var counter = await _context.UsageCounters
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.CompanyId == companyId
                    && c.UsageType == usageType
                    && c.PeriodStartDate == periodStartDate
                    && c.PeriodEndDate == periodEndDate
                    && c.IsActive == true
                    && c.Status == UsageCounterStatusEnum.Active);

            if (counter == null)
                return true; // No counter = no limit yet

            return counter.Used < counter.Limit;
        }

        public async Task ResetAllUsageCountersAsync(int companyId)
        {
            var counters = await _context.UsageCounters
                .Where(c => c.CompanyId == companyId 
                    && c.IsActive == true 
                    && c.Status == UsageCounterStatusEnum.Active)
                .ToListAsync();

            foreach (var counter in counters)
            {
                // Archive: Set Status = Archived but keep IsActive = true (not soft delete)
                counter.Status = UsageCounterStatusEnum.Archived;
                counter.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Archive current usage counters and reset usage to 0 for active period.
        /// This preserves historical data while resetting the usage count.
        /// Used when subscription changes (upgrade/downgrade/cancel).
        /// </summary>
        public async Task ArchiveAndResetUsageCountersAsync(int companyId)
        {
            // Only archive counters that are active (not soft deleted) and have Active status
            var activeCounters = await _context.UsageCounters
                .Where(c => c.CompanyId == companyId 
                    && c.IsActive == true 
                    && c.Status == UsageCounterStatusEnum.Active)
                .ToListAsync();

            foreach (var counter in activeCounters)
            {
                // Archive: Set Status = Archived but KEEP IsActive = true (not soft delete)
                // This preserves the record for dashboard tracking while marking it as archived
                counter.Status = UsageCounterStatusEnum.Archived;
                counter.UpdatedAt = DateTime.UtcNow;
                
                // Create new counter for same period with reset usage
                var newCounter = new UsageCounter
                {
                    CompanyId = counter.CompanyId,
                    CompanySubscriptionId = counter.CompanySubscriptionId, // Will be updated by GetOrCreateCounterAsync
                    UsageType = counter.UsageType,
                    PeriodStartDate = counter.PeriodStartDate,
                    PeriodEndDate = counter.PeriodEndDate,
                    Used = 0, // Reset to 0
                    Limit = counter.Limit, // Will be updated by GetOrCreateCounterAsync with new subscription limit
                    Status = UsageCounterStatusEnum.Active,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _context.UsageCounters.AddAsync(newCounter);
            }
        }
    }
}
