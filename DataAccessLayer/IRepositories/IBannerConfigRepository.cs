using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IBannerConfigRepository
    {
        Task<List<BannerConfig>> GetBannersAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalBannersAsync(string? search = null);
        Task<BannerConfig?> GetByIdAsync(int id);
        Task<BannerConfig> AddAsync(BannerConfig bannerConfig);
        Task UpdateAsync(BannerConfig bannerConfig);
        Task<bool> ExistsAsync(int bannerId);
    }
}

