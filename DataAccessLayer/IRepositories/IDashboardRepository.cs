using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IDashboardRepository
    {
        Task<List<(int CategoryId, string CategoryName, int SpecializationId, string SpecializationName, int ResumeCount)>> GetTopCategorySpecByResumeCountAsync(int companyId, int top = 10);
        Task<int> GetActiveJobsCountAsync(int companyId);
        Task<int> GetTotalCandidatesCountAsync(int companyId);
        Task<int> GetTotalMembersCountAsync(int companyId);
        Task<int> GetAiProcessedCountAsync(int companyId);
        Task<List<(string Name, string JobTitle, decimal Score, Data.Enum.ResumeStatusEnum Status)>> GetTopRatedCandidatesAsync(int companyId, int limit = 5);
        Task<int> GetTotalCompaniesAsync();
        Task<int> GetTotalCompaniesByStatusAsync(Data.Enum.CompanyStatusEnum status);
        Task<int> GetNewCompaniesThisMonthAsync();
        Task<int> GetTotalUsersAsync();
        Task<int> GetTotalJobsAsync();
        Task<int> GetTotalResumesAsync();
        Task<int> GetTotalCompanySubscriptionsAsync();
        Task<int> GetTotalSubscriptionsAsync();
        Task<decimal> GetTotalRevenueAsync();
        Task<List<(int CompanyId, string CompanyName, int ResumeCount, int JobCount)>> GetTopCompaniesByResumeAndJobAsync(int top);
        Task<int> GetCompanySubTotalActiveExpiredAsync();
        Task<int> GetCompanySubCountByStatusAsync(Data.Enum.SubscriptionStatusEnum status);
        Task<int> GetCompanySubNewThisMonthAsync();
        Task<decimal> GetRevenueByRangeAsync(DateTime fromDate, DateTime toDate);
        Task<decimal> GetRevenueFromNewSubscriptionsAsync(DateTime fromDate, DateTime toDate);
        Task<int> GetActiveUsersCountAsync();
        Task<int> GetLockedUsersCountAsync();
        Task<int> GetNewUsersThisMonthAsync();
        Task<List<(string RoleName, int Count)>> GetUsersCountByRoleAsync();
        Task<int> GetTotalJobsAsync(bool onlyActive = true);
        Task<int> GetJobsCountByStatusAsync(Data.Enum.JobStatusEnum status);
        Task<int> GetNewJobsThisMonthAsync();
        Task<int> GetTotalResumesAsync(bool onlyActive = true);
        Task<int> GetNewResumesThisMonthAsync();
        Task<int> GetAppliedResumesThisMonthAsync();
        Task<List<(int SubscriptionId, string SubscriptionName, int ActiveCount, decimal MonthlyRevenue)>> GetSubscriptionPlanBreakdownAsync(DateTime fromDate, DateTime toDate);
        Task<int> GetResumeCountByStatusAsync(Data.Enum.ResumeStatusEnum status);
        Task<int> GetResumeCountByStatusesAsync(IEnumerable<Data.Enum.ResumeStatusEnum> statuses);
    }
}

