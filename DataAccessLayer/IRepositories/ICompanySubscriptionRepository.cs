using Data.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanySubscriptionRepository
    {
        Task<List<CompanySubscription>> GetCompanySubscriptionsAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalCompanySubscriptionsAsync(string? search = null);
        Task<CompanySubscription?> GetByIdAsync(int id);
        Task<CompanySubscription?> GetActiveSubscriptionAsync(int companyId, int subscriptionId);
        Task<CompanySubscription?> GetAnyActiveSubscriptionByCompanyAsync(int companyId);
        Task<CompanySubscription> AddAsync(CompanySubscription companySubscription);
        Task UpdateAsync(CompanySubscription companySubscription);
        Task SoftDeleteAsync(CompanySubscription companySubscription);
    }
}

