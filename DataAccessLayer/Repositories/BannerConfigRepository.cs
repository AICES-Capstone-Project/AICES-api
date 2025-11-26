using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class BannerConfigRepository : IBannerConfigRepository
    {
        private readonly AICESDbContext _context;

        public BannerConfigRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<BannerConfig>> GetBannersAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.BannerConfigs
                .AsNoTracking()
                .Where(b => b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search));
            }

            return await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalBannersAsync(string? search = null)
        {
            var query = _context.BannerConfigs
                .AsNoTracking()
                .Where(b => b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search));
            }

            return await query.CountAsync();
        }

        public async Task<BannerConfig?> GetByIdAsync(int id)
        {
            return await _context.BannerConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BannerId == id);
        }

        public async Task<BannerConfig?> GetForUpdateAsync(int id)
        {
            return await _context.BannerConfigs
                .FirstOrDefaultAsync(b => b.BannerId == id);
        }

        public async Task<BannerConfig> AddAsync(BannerConfig bannerConfig)
        {
            await _context.BannerConfigs.AddAsync(bannerConfig);
            return bannerConfig;
        }

        public async Task UpdateAsync(BannerConfig bannerConfig)
        {
            _context.BannerConfigs.Update(bannerConfig);
        }

        public async Task<bool> ExistsAsync(int bannerId)
        {
            return await _context.BannerConfigs
                .AsNoTracking()
                .AnyAsync(b => b.BannerId == bannerId && b.IsActive);
        }
    }
}

