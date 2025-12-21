using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Response;

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
            var result = await (from ra in _context.ResumeApplications
                               join r in _context.Resumes on ra.ResumeId equals r.ResumeId
                               join j in _context.Jobs on ra.JobId equals j.JobId
                               join s in _context.Specializations on j.SpecializationId equals s.SpecializationId
                               join c in _context.Categories on s.CategoryId equals c.CategoryId
                               where ra.IsActive && r.IsActive && r.CompanyId == companyId 
                                  && j.IsActive 
                                  && j.SpecializationId != null
                               group r by new { c.CategoryId, CategoryName = c.Name, s.SpecializationId, SpecializationName = s.Name } into g
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
            // Count unique candidates (1 người chỉ được tính 1 lần) dựa trên email trong Candidates,
            // join với Resumes để filter theo CompanyId.
            return await (from r in _context.Resumes
                          join c in _context.Candidates on r.CandidateId equals c.CandidateId
                          where r.CompanyId == companyId && r.IsActive
                          select c.Email.ToLower())
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
            return await _context.ResumeApplications
                .AsNoTracking()
                .Join(_context.Resumes, ra => ra.ResumeId, r => r.ResumeId, (ra, r) => new { ra, r })
                .Where(x => x.r.CompanyId == companyId 
                         && x.r.IsActive 
                         && x.ra.IsActive
                         && (x.ra.TotalScore != null || x.ra.AdjustedScore != null))
                .Select(x => x.r.ResumeId)
                .Distinct()
                .CountAsync();
        }

        public async Task<List<(string Name, string JobTitle, decimal Score, Data.Enum.ResumeStatusEnum Status)>> GetTopRatedCandidatesAsync(int companyId, int limit = 5)
        {
            var result = await (from ra in _context.ResumeApplications
                               join r in _context.Resumes on ra.ResumeId equals r.ResumeId
                               join c in _context.Candidates on r.CandidateId equals c.CandidateId
                               join j in _context.Jobs on ra.JobId equals j.JobId
                               where r.CompanyId == companyId 
                                  && r.IsActive
                                  && ra.IsActive
                                  && (ra.TotalScore != null || ra.AdjustedScore != null)
                               orderby (ra.AdjustedScore ?? ra.TotalScore) descending, r.CreatedAt descending
                               select new
                               {
                                   Name = c.FullName,
                                   JobTitle = j.Title,
                                   Score = ra.AdjustedScore ?? ra.TotalScore ?? 0m,
                                   Status = r.Status
                               })
                               .Take(limit)
                               .ToListAsync();

            return result.Select(x => (x.Name, x.JobTitle, x.Score, x.Status)).ToList();
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
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive)
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
                    ResumeCount = _context.Resumes
                        .Where(r => r.CompanyId == c.CompanyId && r.IsActive)
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
            var query = _context.Resumes.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(r => r.IsActive);
            return await query.CountAsync();
        }

        public async Task<int> GetNewResumesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= startOfMonth)
                .CountAsync();
        }

        public async Task<int> GetResumeCountByStatusAsync(ResumeStatusEnum status)
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && r.Status == status)
                .CountAsync();
        }

        public async Task<int> GetResumeCountByStatusesAsync(IEnumerable<ResumeStatusEnum> statuses)
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && statuses.Contains(r.Status))
                .CountAsync();
        }

        public async Task<int> GetAppliedResumesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= startOfMonth
                    && r.Status == ResumeStatusEnum.Completed)
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

        public async Task<PipelineFunnelResponse> GetPipelineFunnelAsync(int companyId, int? jobId, int? campaignId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.ResumeApplications
                .AsNoTracking()
                .Include(ra => ra.Resume)
                .Where(ra => ra.Resume != null && ra.Resume.CompanyId == companyId && ra.IsActive);

            if (jobId.HasValue) query = query.Where(ra => ra.JobId == jobId.Value);
            if (campaignId.HasValue) query = query.Where(ra => ra.CampaignId == campaignId.Value);
            if (startDate.HasValue) query = query.Where(ra => ra.CreatedAt >= startDate.Value);
            if (endDate.HasValue) query = query.Where(ra => ra.CreatedAt <= endDate.Value);

            var data = await query.GroupBy(ra => ra.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalApplied = await query.CountAsync();

            var stages = new List<PipelineStageResponse>();
            
            // "Applied" is total active applications in the set
            stages.Add(new PipelineStageResponse { Name = "Applied", Count = totalApplied, ConversionRate = 100 });
            
            int reviewedCount = data.Where(d => d.Status == ApplicationStatusEnum.Reviewed || d.Status == ApplicationStatusEnum.Shortlisted || d.Status == ApplicationStatusEnum.Interview || d.Status == ApplicationStatusEnum.OfferSent || d.Status == ApplicationStatusEnum.Hired).Sum(d => d.Count);
            stages.Add(new PipelineStageResponse { Name = "Reviewed", Count = reviewedCount, ConversionRate = totalApplied > 0 ? Math.Round((decimal)reviewedCount / totalApplied * 100, 2) : 0 });

            int shortlistedCount = data.Where(d => d.Status == ApplicationStatusEnum.Shortlisted || d.Status == ApplicationStatusEnum.Interview || d.Status == ApplicationStatusEnum.OfferSent || d.Status == ApplicationStatusEnum.Hired).Sum(d => d.Count);
            stages.Add(new PipelineStageResponse { Name = "Shortlisted", Count = shortlistedCount, ConversionRate = totalApplied > 0 ? Math.Round((decimal)shortlistedCount / totalApplied * 100, 2) : 0 });

            int interviewCount = data.Where(d => d.Status == ApplicationStatusEnum.Interview || d.Status == ApplicationStatusEnum.OfferSent || d.Status == ApplicationStatusEnum.Hired).Sum(d => d.Count);
            stages.Add(new PipelineStageResponse { Name = "Interview", Count = interviewCount, ConversionRate = totalApplied > 0 ? Math.Round((decimal)interviewCount / totalApplied * 100, 2) : 0 });

            int offerSentCount = data.Where(d => d.Status == ApplicationStatusEnum.OfferSent || d.Status == ApplicationStatusEnum.Hired).Sum(d => d.Count);
            stages.Add(new PipelineStageResponse { Name = "Offer Sent", Count = offerSentCount, ConversionRate = totalApplied > 0 ? Math.Round((decimal)offerSentCount / totalApplied * 100, 2) : 0 });

            int hiredCount = data.Where(d => d.Status == ApplicationStatusEnum.Hired).Sum(d => d.Count);
            stages.Add(new PipelineStageResponse { Name = "Hired", Count = hiredCount, ConversionRate = totalApplied > 0 ? Math.Round((decimal)hiredCount / totalApplied * 100, 2) : 0 });

            return new PipelineFunnelResponse { Stages = stages };
        }

        public async Task<UsageHistoryResponse> GetUsageHistoryAsync(int companyId, string range)
        {
            DateTime now = DateTime.UtcNow;
            DateTime from;
            string unit = "day";
            bool groupByMonth = false;
            bool groupByHour = false;

            if (range.Equals("1d", StringComparison.OrdinalIgnoreCase))
            {
                from = now.AddHours(-23);
                unit = "hour";
                groupByHour = true;
            }
            else if (range.Equals("7d", StringComparison.OrdinalIgnoreCase))
            {
                from = now.AddDays(-6).Date;
                unit = "day";
            }
            else if (range.Equals("28d", StringComparison.OrdinalIgnoreCase))
            {
                from = now.AddDays(-27).Date;
                unit = "day";
            }
            else if (range.Equals("90d", StringComparison.OrdinalIgnoreCase))
            {
                from = now.AddDays(-89).Date;
                unit = "day";
            }
            else if (range.Equals("month", StringComparison.OrdinalIgnoreCase))
            {
                from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                unit = "day";
            }
            else if (range.Equals("year", StringComparison.OrdinalIgnoreCase))
            {
                from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
                unit = "month";
                groupByMonth = true;
            }
            else // Default to 28 days
            {
                from = now.AddDays(-27).Date;
                unit = "day";
            }

            // Aggregate data from UsageCounters (Primary source for billing/limit consistency)
            var usageData = await _context.UsageCounters
                .AsNoTracking()
                .Where(uc => uc.CompanyId == companyId 
                          && uc.PeriodStartDate >= from 
                          && uc.PeriodStartDate <= now 
                          && uc.IsActive)
                .ToListAsync();

            // Get subscription limits
            var companySubscription = await _context.CompanySubscriptions
                .Include(cs => cs.Subscription)
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.IsActive && cs.SubscriptionStatus == Data.Enum.SubscriptionStatusEnum.Active);

            var resumeLimit = companySubscription?.Subscription?.ResumeLimit ?? 0;
            var aiComparisonLimit = companySubscription?.Subscription?.CompareLimit;

            var labels = new List<string>();
            var resumeUploads = new List<int>();
            var aiComparisons = new List<int>();

            if (groupByMonth)
            {
                for (var dt = new DateTime(from.Year, from.Month, 1); dt <= now; dt = dt.AddMonths(1))
                {
                    labels.Add(dt.ToString("MMM yyyy"));
                    resumeUploads.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Resume && u.PeriodStartDate.Year == dt.Year && u.PeriodStartDate.Month == dt.Month).Sum(u => u.Used));
                    aiComparisons.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Comparison && u.PeriodStartDate.Year == dt.Year && u.PeriodStartDate.Month == dt.Month).Sum(u => u.Used));
                }
            }
            else if (groupByHour)
            {
                for (var dt = from; dt <= now; dt = dt.AddHours(1))
                {
                    labels.Add(dt.ToString("HH:00"));
                    resumeUploads.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Resume && u.PeriodStartDate.Date == dt.Date && u.PeriodStartDate.Hour == dt.Hour).Sum(u => u.Used));
                    aiComparisons.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Comparison && u.PeriodStartDate.Date == dt.Date && u.PeriodStartDate.Hour == dt.Hour).Sum(u => u.Used));
                }
            }
            else
            {
                for (var dt = from.Date; dt <= now.Date; dt = dt.AddDays(1))
                {
                    labels.Add(dt.ToString("MMM dd"));
                    resumeUploads.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Resume && u.PeriodStartDate.Date == dt.Date).Sum(u => u.Used));
                    aiComparisons.Add(usageData.Where(u => u.UsageType == UsageTypeEnum.Comparison && u.PeriodStartDate.Date == dt.Date).Sum(u => u.Used));
                }
            }

            return new UsageHistoryResponse
            {
                Range = range,
                Unit = unit,
                Labels = labels,
                ResumeUploads = resumeUploads,
                AiComparisons = aiComparisons,
                ResumeLimit = resumeLimit,
                AiComparisonLimit = aiComparisonLimit
            };
        }

        public async Task<CompanyStatsOverviewResponse> GetCompanyStatsOverviewAsync(int companyId)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            var totalJobs = await _context.Jobs.CountAsync(j => j.CompanyId == companyId && j.IsActive);

            var top5JobsInCampaigns = await _context.JobCampaigns
                .Include(jc => jc.Job)
                .Where(jc => jc.Job != null && jc.Job.CompanyId == companyId && jc.Job.IsActive)
                .GroupBy(jc => new { jc.JobId, Title = jc.Job!.Title })
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new Top5JobInCampaignResponse { JobId = g.Key.JobId, Title = g.Key.Title, CampaignCount = g.Count() })
                .ToListAsync();

            var top5CampaignsWithMostJobs = await _context.JobCampaigns
                .Include(jc => jc.Campaign)
                .Where(jc => jc.Campaign != null && jc.Campaign.CompanyId == companyId && jc.Campaign.IsActive)
                .GroupBy(jc => new { jc.CampaignId, Title = jc.Campaign!.Title })
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new Top5CampaignWithMostJobsResponse { CampaignId = g.Key.CampaignId, Title = g.Key.Title, JobCount = g.Count() })
                .ToListAsync();

            var top5CandidatesWithMostJobs = await _context.ResumeApplications
                .Include(ra => ra.Resume)
                .Include(ra => ra.Candidate)
                .Where(ra => ra.Resume != null && ra.Resume.CompanyId == companyId && ra.IsActive && ra.CandidateId != null && ra.Candidate != null)
                .GroupBy(ra => new { ra.CandidateId, FullName = ra.Candidate!.FullName })
                .OrderByDescending(g => g.Select(ra => ra.JobId).Distinct().Count())
                .Take(5)
                .Select(g => new Top5CandidateWithMostJobsResponse { CandidateId = g.Key.CandidateId!.Value, FullName = g.Key.FullName, JobCount = g.Select(ra => ra.JobId).Distinct().Count() })
                .ToListAsync();

            var top5HighestScoreCVs = await _context.ResumeApplications
                .Include(ra => ra.Resume)
                .Include(ra => ra.Candidate)
                .Include(ra => ra.Job)
                .Where(ra => ra.Resume != null && ra.Resume.CompanyId == companyId && ra.IsActive && ra.TotalScore != null && ra.Candidate != null && ra.Job != null)
                .OrderByDescending(ra => ra.TotalScore)
                .Take(5)
                .Select(ra => new Top5HighestScoreCVResponse { ApplicationId = ra.ApplicationId, CandidateName = ra.Candidate!.FullName, JobTitle = ra.Job!.Title, Score = ra.TotalScore!.Value })
                .ToListAsync();

            var campaigns = await _context.Campaigns
                .Include(c => c.JobCampaigns)
                .Include(c => c.ResumeApplications)
                .Where(c => c.CompanyId == companyId && c.IsActive && c.EndDate >= startOfMonth && c.EndDate <= endOfMonth)
                .ToListAsync();

            var onTimeCampaignsCount = campaigns.Count(c => 
                c.ResumeApplications.Count(ra => ra.Status == ApplicationStatusEnum.Hired && ra.IsActive) >= (c.JobCampaigns?.Sum(jc => jc.TargetQuantity) ?? 0)
            );

            return new CompanyStatsOverviewResponse
            {
                TotalJobs = totalJobs,
                Top5JobsInCampaigns = top5JobsInCampaigns,
                Top5CampaignsWithMostJobs = top5CampaignsWithMostJobs,
                Top5CandidatesWithMostJobs = top5CandidatesWithMostJobs,
                Top5HighestScoreCVs = top5HighestScoreCVs,
                OnTimeCampaignsThisMonth = onTimeCampaignsCount
            };
        }
    }
}

