using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly AICESDbContext _context;

        public ReportRepository(AICESDbContext context)
        {
            _context = context;
        }

        #region Executive Summary

        public async Task<int> GetTotalActiveCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .CountAsync();
        }

        public async Task<int> GetActiveCompaniesWithJobsOrSubscriptionsAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       (_context.Jobs.Any(j => j.CompanyId == c.CompanyId && j.IsActive && j.JobStatus == JobStatusEnum.Published) ||
                        _context.CompanySubscriptions.Any(cs => cs.CompanyId == c.CompanyId && cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)))
                .CountAsync();
        }

        public async Task<int> GetTotalActiveJobsAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive)
                .CountAsync();
        }

        public async Task<int> GetAiProcessedResumesCountAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && (ra.TotalScore != null || ra.AdjustedScore != null))
                .Select(ra => ra.ResumeId)
                .Distinct()
                .CountAsync();
        }

        public async Task<decimal> GetTotalRevenueFromPaidPaymentsAsync()
        {
            var totalInCents = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.IsActive &&
                       t.Payment != null &&
                       t.Payment.IsActive &&
                       t.Payment.PaymentStatus == PaymentStatusEnum.Paid)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
            // Convert cents to dollars
            return totalInCents / 100;
        }

        public async Task<int> GetCompaniesWithSubscriptionsCountAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Select(cs => cs.CompanyId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetCompaniesWithMultipleSubscriptionsCountAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .GroupBy(cs => cs.CompanyId)
                .Where(g => g.Count() > 1)
                .CountAsync();
        }

        #endregion

        #region Companies Overview

        public async Task<int> GetTotalActiveCompaniesCountAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .CountAsync();
        }

        public async Task<int> GetCompaniesByStatusAsync(CompanyStatusEnum status)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyStatus == status)
                .CountAsync();
        }

        public async Task<int> GetNewCompaniesThisMonthAsync(DateTime firstDayOfMonth)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CreatedAt >= firstDayOfMonth)
                .CountAsync();
        }

        public async Task<int> GetCompaniesWithActiveSubscriptionAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active))
                .CountAsync();
        }

        public async Task<int> GetCompaniesWithExpiredSubscriptionAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Expired) &&
                       !c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active))
                .CountAsync();
        }

        public async Task<int> GetCompaniesWithoutSubscriptionAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       !c.CompanySubscriptions.Any(cs => cs.IsActive))
                .CountAsync();
        }

        #endregion

        #region Companies Usage

        public async Task<int> GetActiveCompaniesWithContentAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanyStatus == CompanyStatusEnum.Approved &&
                       (c.Jobs.Any(j => j.IsActive) || c.Resumes.Any(r => r.IsActive)))
                .CountAsync();
        }

        public async Task<int> GetFrequentCompaniesAsync(DateTime since)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanyStatus == CompanyStatusEnum.Approved &&
                       (c.Jobs.Any(j => j.IsActive && j.CreatedAt >= since) ||
                        c.Resumes.Any(r => r.IsActive && r.CreatedAt >= since) ||
                        c.Jobs.Any(j => j.IsActive && j.ResumeApplications.Any(ra => ra.IsActive && ra.CreatedAt >= since))))
                .CountAsync();
        }

        public async Task<int> GetCompaniesUsingAIAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanyStatus == CompanyStatusEnum.Approved &&
                       c.Resumes.Any(r => r.IsActive &&
                                    r.ResumeApplications.Any(ra => ra.IsActive &&
                                                            (ra.TotalScore != null || ra.AdjustedScore != null))))
                .CountAsync();
        }

        public async Task<int> GetReturningCompaniesAsync(DateTime since)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanyStatus == CompanyStatusEnum.Approved &&
                       (c.Jobs.Any(j => j.IsActive && j.CreatedAt >= since) ||
                        c.Resumes.Any(r => r.IsActive && r.CreatedAt >= since) ||
                        c.Jobs.Any(j => j.IsActive && j.ResumeApplications.Any(ra => ra.IsActive && ra.CreatedAt >= since))))
                .CountAsync();
        }

        #endregion

        #region Jobs Statistics

        public async Task<int> GetTotalActiveJobsCountAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive)
                .CountAsync();
        }

        public async Task<int> GetActiveJobsByStatusAsync(JobStatusEnum status)
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.JobStatus == status)
                .CountAsync();
        }

        public async Task<int> GetNewJobsThisMonthAsync(DateTime firstDayOfMonth)
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.CreatedAt >= firstDayOfMonth)
                .CountAsync();
        }

        public async Task<int> GetTotalApplicationsCountAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive)
                .CountAsync();
        }

        public async Task<int> GetJobsWithApplicationsCountAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.ResumeApplications.Any(ra => ra.IsActive))
                .CountAsync();
        }

        public async Task<List<CategoryJobCount>> GetTopCategoriesByJobCountAsync(int topCount)
        {
            return await (from j in _context.Jobs
                         join s in _context.Specializations on j.SpecializationId equals s.SpecializationId into specGroup
                         from spec in specGroup.DefaultIfEmpty()
                         join c in _context.Categories on spec.CategoryId equals c.CategoryId into catGroup
                         from cat in catGroup.DefaultIfEmpty()
                         where j.IsActive
                         group j by new { CategoryId = cat != null ? cat.CategoryId : 0, CategoryName = cat != null ? cat.Name : "Unknown" } into g
                         orderby g.Count() descending
                         select new CategoryJobCount
                         {
                             CategoryId = g.Key.CategoryId,
                             CategoryName = g.Key.CategoryName,
                             JobCount = g.Count()
                         })
                         .Take(topCount)
                         .ToListAsync();
        }

        #endregion

        #region Jobs Effectiveness

        public async Task<int> GetTotalResumesCountAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive)
                .CountAsync();
        }

        public async Task<int> GetTotalActiveJobsForEffectivenessAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .CountAsync();
        }

        public async Task<int> GetTotalApplicationsWithScoreAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && (ra.TotalScore != null || ra.AdjustedScore != null))
                .CountAsync();
        }

        public async Task<int> GetQualifiedApplicationsCountAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && 
                       ((ra.AdjustedScore != null && ra.AdjustedScore > 75) ||
                        (ra.AdjustedScore == null && ra.TotalScore != null && ra.TotalScore > 75)))
                .CountAsync();
        }

        public async Task<int> GetTotalPublishedJobsCountAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .CountAsync();
        }

        public async Task<int> GetSuccessfulJobsCountAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive &&
                       j.JobStatus == JobStatusEnum.Published &&
                       j.ResumeApplications.Any(ra => ra.IsActive && ra.Status == ApplicationStatusEnum.Hired))
                .CountAsync();
        }

        #endregion

        #region AI Parsing Quality

        public async Task<int> GetTotalResumesForParsingAsync()
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive)
                .CountAsync();
        }

        public async Task<int> GetSuccessfulParsingCountAsync()
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && r.Status == ResumeStatusEnum.Completed)
                .CountAsync();
        }

        public async Task<int> GetFailedParsingCountAsync()
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive &&
                       r.Status != ResumeStatusEnum.Completed &&
                       r.Status != ResumeStatusEnum.Pending)
                .CountAsync();
        }

        public async Task<decimal> GetAverageProcessingTimeFromApplicationsAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && ra.ProcessingTimeMs != null)
                .AverageAsync(ra => (decimal?)ra.ProcessingTimeMs) ?? 0;
        }

        public async Task<List<ErrorStatistic>> GetCommonParsingErrorsAsync(int topCount)
        {
            var failedParsing = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive &&
                       r.Status != ResumeStatusEnum.Completed &&
                       r.Status != ResumeStatusEnum.Pending)
                .CountAsync();

            var errorStats = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive &&
                       r.Status != ResumeStatusEnum.Completed &&
                       r.Status != ResumeStatusEnum.Pending)
                .GroupBy(r => r.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(topCount)
                .ToListAsync();

            return errorStats.Select(e => new ErrorStatistic
            {
                ErrorType = e.Status.ToString(),
                Count = e.Count,
                Percentage = failedParsing > 0 ? Math.Round((decimal)e.Count / failedParsing, 2) : 0
            }).ToList();
        }

        #endregion

        #region AI Scoring Distribution

        public async Task<int> GetTotalScoredApplicationsAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && (ra.TotalScore != null || ra.AdjustedScore != null))
                .CountAsync();
        }

        public async Task<List<decimal>> GetAllScoresAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && (ra.TotalScore != null || ra.AdjustedScore != null))
                .Select(ra => ra.AdjustedScore ?? ra.TotalScore ?? 0)
                .ToListAsync();
        }

        public async Task<int> GetTotalApplicationsForScoringAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive)
                .CountAsync();
        }

        public async Task<decimal> GetAverageProcessingTimeForScoringAsync()
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && ra.ProcessingTimeMs != null && (ra.TotalScore != null || ra.AdjustedScore != null))
                .AverageAsync(ra => (decimal?)ra.ProcessingTimeMs) ?? 0;
        }

        public async Task<List<string>> GetCommonScoringErrorsAsync(int topCount)
        {
            var errorMessages = await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && !string.IsNullOrEmpty(ra.ErrorMessage))
                .Select(ra => ra.ErrorMessage!)
                .ToListAsync();

            return errorMessages
                .Take(topCount)
                .Distinct()
                .ToList();
        }

        #endregion

        #region Subscription Revenue

        public async Task<int> GetTotalActiveCompaniesForSubscriptionAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                .CountAsync();
        }

        public async Task<int> GetPaidCompaniesCountAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive &&
                       c.CompanyStatus == CompanyStatusEnum.Approved &&
                       c.CompanySubscriptions.Any(cs => cs.IsActive &&
                                                   cs.SubscriptionStatus == SubscriptionStatusEnum.Active &&
                                                   cs.Payments.Any(p => p.IsActive && p.PaymentStatus == PaymentStatusEnum.Paid)))
                .CountAsync();
        }

        public async Task<decimal> GetMonthlyRevenueAsync(int year, int month)
        {
            var firstDay = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastDay = firstDay.AddMonths(1);

            var totalInCents = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.IsActive &&
                       t.Payment != null &&
                       t.Payment.IsActive &&
                       t.Payment.PaymentStatus == PaymentStatusEnum.Paid &&
                       t.CreatedAt >= firstDay &&
                       t.CreatedAt < lastDay)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
            // Convert cents to dollars
            return totalInCents / 100;
        }

        public async Task<int> GetCompaniesWithSubscriptionsForRevenueAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)
                .Select(cs => cs.CompanyId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetCompaniesWithMultipleSubscriptionsForRevenueAsync()
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)
                .GroupBy(cs => cs.CompanyId)
                .Where(g => g.Count() > 1)
                .CountAsync();
        }

        public async Task<List<PlanStatistic>> GetPlanStatisticsAsync()
        {
            // Get all active subscriptions
            var allSubscriptions = await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => new
                {
                    s.SubscriptionId,
                    s.Name
                })
                .ToListAsync();

            // Get company count per subscription (only active subscriptions)
            var companyCountDict = await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)
                .GroupBy(cs => cs.SubscriptionId)
                .Select(g => new
                {
                    SubscriptionId = g.Key,
                    CompanyCount = g.Select(x => x.CompanyId).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.SubscriptionId, x => x.CompanyCount);

            // Get revenue per subscription (only paid transactions)
            var planRevenueDict = await (from t in _context.Transactions
                                        join p in _context.Payments on t.PaymentId equals p.PaymentId
                                        join cs in _context.CompanySubscriptions on p.ComSubId equals cs.ComSubId
                                        where t.IsActive &&
                                              p.IsActive &&
                                              p.PaymentStatus == PaymentStatusEnum.Paid &&
                                              cs.IsActive
                                        group t by cs.SubscriptionId into g
                                        select new
                                        {
                                            SubscriptionId = g.Key,
                                            Revenue = g.Sum(x => x.Amount)
                                        })
                                        .ToDictionaryAsync(x => x.SubscriptionId, x => x.Revenue);

            // Combine all data - show all subscriptions even with 0 companies and 0 revenue
            return allSubscriptions.Select(s => new PlanStatistic
            {
                SubscriptionId = s.SubscriptionId,
                PlanName = s.Name,
                CompanyCount = companyCountDict.TryGetValue(s.SubscriptionId, out var count) ? count : 0,
                // Convert cents to dollars
                Revenue = planRevenueDict.TryGetValue(s.SubscriptionId, out var revenue) ? revenue / 100 : 0
            }).ToList();
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            var totalInCents = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.IsActive &&
                       t.Payment != null &&
                       t.Payment.IsActive &&
                       t.Payment.PaymentStatus == PaymentStatusEnum.Paid)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
            // Convert cents to dollars
            return totalInCents / 100;
        }

        #endregion
    }
}
