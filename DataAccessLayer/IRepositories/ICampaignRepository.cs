using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICampaignRepository
    {
        Task<List<Campaign>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetTotalAsync(string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<Campaign?> GetByIdAsync(int id);
        Task<Campaign?> GetForUpdateAsync(int id);
        Task<IEnumerable<Campaign>> GetByCompanyIdAsync(int companyId);
        Task<List<Campaign>> GetByCompanyIdWithFiltersAsync(int companyId, int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetTotalByCompanyIdWithFiltersAsync(int companyId, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task AddAsync(Campaign campaign);
        void Update(Campaign campaign);
        
        // Legacy methods for backward compatibility
        Task UpdateAsync(Campaign campaign);
        Task SoftDeleteAsync(Campaign campaign);
    }
}

