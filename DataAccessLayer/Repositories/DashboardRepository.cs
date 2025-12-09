using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AICESDbContext _context;

        public DashboardRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<(int CategoryId, string CategoryName, int SpecializationId, string SpecializationName, int ResumeCount)>> GetTopCategorySpecByResumeCountAsync(int companyId, int top = 10)
        {
            var result = await (from pr in _context.ParsedResumes
                               join j in _context.Jobs on pr.JobId equals j.JobId
                               join s in _context.Specializations on j.SpecializationId equals s.SpecializationId
                               join c in _context.Categories on s.CategoryId equals c.CategoryId
                               where pr.IsActive && pr.CompanyId == companyId 
                                  && j.IsActive 
                                  && j.SpecializationId != null
                               group pr by new { c.CategoryId, CategoryName = c.Name, s.SpecializationId, SpecializationName = s.Name } into g
                               select new
                               {
                                   g.Key.CategoryId,
                                   g.Key.CategoryName,
                                   g.Key.SpecializationId,
                                   g.Key.SpecializationName,
                                   ResumeCount = g.Count()
                               })
                               .OrderByDescending(x => x.ResumeCount)
                               .Take(top)
                               .ToListAsync();

            return result.Select(x => (x.CategoryId, x.CategoryName, x.SpecializationId, x.SpecializationName, x.ResumeCount)).ToList();
        }

        public async Task<int> GetActiveJobsCountAsync(int companyId)
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.CompanyId == companyId 
                         && j.IsActive 
                         && j.JobStatus == JobStatusEnum.Published)
                .CountAsync();
        }

        public async Task<int> GetTotalCandidatesCountAsync(int companyId)
        {
            // Count unique candidates (1 người chỉ được tính 1 lần) dựa trên email trong ParsedCandidates,
            // join với ParsedResumes để filter theo CompanyId.
            return await (from pr in _context.ParsedResumes
                          join pc in _context.ParsedCandidates on pr.ResumeId equals pc.ResumeId
                          where pr.CompanyId == companyId && pr.IsActive
                          select pc.Email.ToLower())
                         .Distinct()
                         .CountAsync();
        }

        public async Task<int> GetTotalMembersCountAsync(int companyId)
        {
            // Count total members (CompanyUser) currently in the company (Approved & active)
            return await _context.CompanyUsers
                .AsNoTracking()
                .Where(cu => cu.CompanyId == companyId 
                          && cu.IsActive 
                          && cu.JoinStatus == JoinStatusEnum.Approved)
                .CountAsync();
        }

        public async Task<int> GetAiProcessedCountAsync(int companyId)
        {
            return await (from pr in _context.ParsedResumes
                         join pc in _context.ParsedCandidates on pr.ResumeId equals pc.ResumeId
                         join ais in _context.AIScores on pc.CandidateId equals ais.CandidateId
                         where pr.CompanyId == companyId && pr.IsActive
                         select pr.ResumeId)
                         .Distinct()
                         .CountAsync();
        }

        public async Task<List<(string Name, string JobTitle, decimal AIScore, Data.Enum.ResumeStatusEnum Status)>> GetTopRatedCandidatesAsync(int companyId, int limit = 5)
        {
            var result = await (from pr in _context.ParsedResumes
                               join pc in _context.ParsedCandidates on pr.ResumeId equals pc.ResumeId
                               join ais in _context.AIScores on pc.CandidateId equals ais.CandidateId
                               join j in _context.Jobs on pr.JobId equals j.JobId
                               where pr.CompanyId == companyId 
                                  && pr.IsActive
                               orderby ais.TotalResumeScore descending, ais.CreatedAt descending
                               select new
                               {
                                   Name = pc.FullName,
                                   JobTitle = j.Title,
                                   AIScore = ais.TotalResumeScore,
                                   Status = pr.ResumeStatus
                               })
                               .Take(limit)
                               .ToListAsync();

            return result.Select(x => (x.Name, x.JobTitle, x.AIScore, x.Status)).ToList();
        }

        public async Task<int> GetTotalCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalCompaniesByStatusAsync(CompanyStatusEnum status)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyStatus == status)
                .CountAsync();
        }

        public async Task<int> GetNewCompaniesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive
                    && c.CreatedAt.HasValue
                    && c.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<int> GetTotalUsersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalJobsAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalResumesAsync()
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(pr => pr.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalCompanySubscriptionsAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalSubscriptionsAsync()
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.IsActive)
                .CountAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            var total = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Payment.IsActive && t.Payment.PaymentStatus == PaymentStatusEnum.Paid)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return total;
        }

        public async Task<List<(int CompanyId, string CompanyName, int ResumeCount, int JobCount)>> GetTopCompaniesByResumeAndJobAsync(int top)
        {
            var query = await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new
                {
                    c.CompanyId,
                    CompanyName = c.Name,
                    ResumeCount = _context.ParsedResumes
                        .Where(pr => pr.CompanyId == c.CompanyId && pr.IsActive)
                        .Count(),
                    JobCount = _context.Jobs
                        .Where(j => j.CompanyId == c.CompanyId && j.IsActive)
                        .Count()
                })
                .OrderByDescending(x => x.ResumeCount)
                .ThenByDescending(x => x.JobCount)
                .Take(top)
                .ToListAsync();

            return query.Select(x => (x.CompanyId, x.CompanyName, x.ResumeCount, x.JobCount)).ToList();
        }

        public async Task<int> GetCompanySubTotalActiveExpiredAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive &&
                             (cs.SubscriptionStatus == SubscriptionStatusEnum.Active ||
                              cs.SubscriptionStatus == SubscriptionStatusEnum.Expired))
                .CountAsync();
        }

        public async Task<int> GetCompanySubCountByStatusAsync(SubscriptionStatusEnum status)
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive && cs.SubscriptionStatus == status)
                .CountAsync();
        }

        public async Task<int> GetCompanySubNewThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive
                    && cs.CreatedAt.HasValue
                    && cs.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<decimal> GetRevenueByRangeAsync(DateTime fromDate, DateTime toDate)
        {
            var total = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Payment.IsActive
                    && t.Payment.PaymentStatus == PaymentStatusEnum.Paid
                    && t.TransactionTime.HasValue
                    && t.TransactionTime.Value >= fromDate
                    && t.TransactionTime.Value < toDate)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return total;
        }

        public async Task<decimal> GetRevenueFromNewSubscriptionsAsync(DateTime fromDate, DateTime toDate)
        {
            var total = await _context.Transactions
                .AsNoTracking()
                .Where(t =>
                    t.Payment.IsActive &&
                    t.Payment.PaymentStatus == PaymentStatusEnum.Paid &&
                    t.TransactionTime.HasValue &&
                    t.TransactionTime.Value >= fromDate &&
                    t.TransactionTime.Value < toDate &&
                    t.Payment.CompanySubscription != null &&
                    t.Payment.CompanySubscription.IsActive &&
                    t.Payment.CompanySubscription.CreatedAt.HasValue &&
                    t.Payment.CompanySubscription.CreatedAt.Value >= fromDate &&
                    t.Payment.CompanySubscription.CreatedAt.Value < toDate)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return total;
        }

        public async Task<int> GetActiveUsersCountAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Status == UserStatusEnum.Verified)
                .CountAsync();
        }

        public async Task<int> GetLockedUsersCountAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Status == UserStatusEnum.Locked)
                .CountAsync();
        }

        public async Task<int> GetNewUsersThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive
                    && u.CreatedAt.HasValue
                    && u.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<List<(string RoleName, int Count)>> GetUsersCountByRoleAsync()
        {
            var data = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.IsActive)
                .GroupBy(u => u.Role.RoleName)
                .Select(g => new
                {
                    RoleName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return data.Select(x => (x.RoleName, x.Count)).ToList();
        }

        public async Task<int> GetTotalJobsAsync(bool onlyActive = true)
        {
            var query = _context.Jobs.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(j => j.IsActive);
            return await query.CountAsync();
        }

        public async Task<int> GetJobsCountByStatusAsync(JobStatusEnum status)
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.JobStatus == status)
                .CountAsync();
        }

        public async Task<int> GetNewJobsThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive
                    && j.CreatedAt.HasValue
                    && j.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<int> GetTotalResumesAsync(bool onlyActive = true)
        {
            var query = _context.ParsedResumes.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(pr => pr.IsActive);
            return await query.CountAsync();
        }

        public async Task<int> GetNewResumesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(pr => pr.IsActive
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<int> GetAppliedResumesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(pr => pr.IsActive
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= startOfMonth
                    && pr.ResumeStatus == ResumeStatusEnum.Completed)
                .CountAsync();
        }

        public async Task<List<(int SubscriptionId, string SubscriptionName, int ActiveCount, decimal MonthlyRevenue)>> GetSubscriptionPlanBreakdownAsync(DateTime fromDate, DateTime toDate)
        {
            var now = DateTime.UtcNow;

            // Active subscriptions per plan
            var activeByPlan = await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive
                    && cs.SubscriptionStatus == SubscriptionStatusEnum.Active
                    && cs.EndDate > now)
                .GroupBy(cs => cs.SubscriptionId)
                .Select(g => new { SubscriptionId = g.Key, ActiveCount = g.Count() })
                .ToListAsync();

            // Monthly revenue per plan (transactions paid in range)
            var revenueByPlan = await (from t in _context.Transactions.AsNoTracking()
                                       join p in _context.Payments.AsNoTracking() on t.PaymentId equals p.PaymentId
                                       join cs in _context.CompanySubscriptions.AsNoTracking() on p.ComSubId equals cs.ComSubId into csJoin
                                       from cs in csJoin.DefaultIfEmpty()
                                       where t.Payment.IsActive
                                             && p.PaymentStatus == PaymentStatusEnum.Paid
                                             && t.TransactionTime.HasValue
                                             && t.TransactionTime.Value >= fromDate
                                             && t.TransactionTime.Value < toDate
                                             && cs != null
                                       group t by cs.SubscriptionId into g
                                       select new
                                       {
                                           SubscriptionId = g.Key,
                                           Revenue = g.Sum(x => (decimal?)x.Amount) ?? 0m
                                       }).ToListAsync();

            var subs = await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => new { s.SubscriptionId, s.Name })
                .ToListAsync();

            var activeDict = activeByPlan.ToDictionary(x => x.SubscriptionId, x => x.ActiveCount);
            var revenueDict = revenueByPlan.ToDictionary(x => x.SubscriptionId, x => x.Revenue);

            var result = subs.Select(s => (
                s.SubscriptionId,
                s.Name,
                activeDict.TryGetValue(s.SubscriptionId, out var ac) ? ac : 0,
                revenueDict.TryGetValue(s.SubscriptionId, out var rev) ? rev : 0m
            )).ToList();

            return result;
        }
    }
}

