using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IComparisonRepository
    {
        Task<Comparison> CreateAsync(Comparison comparison);
        Task<Comparison?> GetByIdAsync(int comparisonId);
        Task<Comparison?> GetByIdWithApplicationsAsync(int comparisonId);
        Task<List<Comparison>> GetByJobIdAndCampaignIdAsync(int jobId, int campaignId);
        Task UpdateAsync(Comparison comparison);
        Task<bool> IsDuplicateComparisonAsync(int companyId, List<int> applicationIds);
    }
}
