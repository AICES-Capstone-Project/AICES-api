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
                    && c.IsActive);

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
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.UsageCounters.AddAsync(counter);
            }
            else
            {
                // Update limit if changed (e.g., subscription upgraded)
                if (counter.Limit != limit)
                {
                    counter.Limit = limit;
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
                    && c.IsActive);
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
                    && c.IsActive);

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
                    && c.IsActive);

            if (counter == null)
                return true; // No counter = no limit yet

            return counter.Used < counter.Limit;
        }

        public async Task ResetAllUsageCountersAsync(int companyId)
        {
            var counters = await _context.UsageCounters
                .Where(c => c.CompanyId == companyId && c.IsActive)
                .ToListAsync();

            foreach (var counter in counters)
            {
                counter.IsActive = false;
                counter.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
