using Data.Enum;
using Data.Models.Response;

namespace DataAccessLayer.IRepositories
{
    public interface IReportRepository
    {
        // Executive Summary
        Task<int> GetTotalActiveCompaniesAsync();
        Task<int> GetActiveCompaniesWithJobsOrSubscriptionsAsync();
        Task<int> GetTotalActiveJobsAsync();
        Task<int> GetAiProcessedResumesCountAsync();
        Task<decimal> GetTotalRevenueFromPaidPaymentsAsync();
        Task<int> GetCompaniesWithSubscriptionsCountAsync();
        Task<int> GetCompaniesWithMultipleSubscriptionsCountAsync();

        // Companies Overview
        Task<int> GetTotalActiveCompaniesCountAsync();
        Task<int> GetCompaniesByStatusAsync(CompanyStatusEnum status);
        Task<int> GetNewCompaniesThisMonthAsync(DateTime firstDayOfMonth);
        Task<int> GetCompaniesWithActiveSubscriptionAsync();
        Task<int> GetCompaniesWithExpiredSubscriptionAsync();
        Task<int> GetCompaniesWithoutSubscriptionAsync();

        // Companies Usage
        Task<int> GetActiveCompaniesWithContentAsync();
        Task<int> GetFrequentCompaniesAsync(DateTime since);
        Task<int> GetCompaniesUsingAIAsync();
        Task<int> GetReturningCompaniesAsync(DateTime since);

        // Jobs Statistics
        Task<int> GetTotalActiveJobsCountAsync();
        Task<int> GetActiveJobsByStatusAsync(JobStatusEnum status);
        Task<int> GetNewJobsThisMonthAsync(DateTime firstDayOfMonth);
        Task<int> GetTotalApplicationsCountAsync();
        Task<int> GetJobsWithApplicationsCountAsync();
        Task<List<CategoryJobCount>> GetTopCategoriesByJobCountAsync(int topCount);

        // Jobs Effectiveness
        Task<int> GetTotalResumesCountAsync();
        Task<int> GetTotalActiveJobsForEffectivenessAsync();
        Task<int> GetTotalApplicationsWithScoreAsync();
        Task<int> GetQualifiedApplicationsCountAsync();
        Task<int> GetTotalPublishedJobsCountAsync();
        Task<int> GetSuccessfulJobsCountAsync();

        // AI Parsing Quality
        Task<int> GetTotalResumesForParsingAsync();
        Task<int> GetSuccessfulParsingCountAsync();
        Task<int> GetFailedParsingCountAsync();
        Task<decimal> GetAverageProcessingTimeFromApplicationsAsync();
        Task<List<ErrorStatistic>> GetCommonParsingErrorsAsync(int topCount);

        // AI Scoring Distribution
        Task<int> GetTotalScoredApplicationsAsync();
        Task<List<decimal>> GetAllScoresAsync();
        Task<int> GetTotalApplicationsForScoringAsync();
        Task<decimal> GetAverageProcessingTimeForScoringAsync();
        Task<List<string>> GetCommonScoringErrorsAsync(int topCount);

        // Subscription Revenue
        Task<int> GetTotalActiveCompaniesForSubscriptionAsync();
        Task<int> GetPaidCompaniesCountAsync();
        Task<decimal> GetMonthlyRevenueAsync(int year, int month);
        Task<int> GetCompaniesWithSubscriptionsForRevenueAsync();
        Task<int> GetCompaniesWithMultipleSubscriptionsForRevenueAsync();
        Task<List<PlanStatistic>> GetPlanStatisticsAsync();
        Task<decimal> GetTotalRevenueAsync();
    }
}
