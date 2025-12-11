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
    public class CampaignRepository : ICampaignRepository
    {
        private readonly AICESDbContext _context;

        public CampaignRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<Campaign>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Company != null && c.Company.Name != null && c.Company.Name.Contains(search)));
            }

            // Filter by status
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            // Filter by start date (campaigns that start on or after this date)
            if (startDate.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDate.Value);
            }

            // Filter by end date (campaigns that end on or before this date)
            if (endDate.HasValue)
            {
                query = query.Where(c => c.EndDate <= endDate.Value);
            }

            return await query
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalAsync(string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Company != null && c.Company.Name != null && c.Company.Name.Contains(search)));
            }

            // Filter by status
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            // Filter by start date
            if (startDate.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDate.Value);
            }

            // Filter by end date
            if (endDate.HasValue)
            {
                query = query.Where(c => c.EndDate <= endDate.Value);
            }

            return await query.CountAsync();
        }

        public async Task<Campaign?> GetByIdAsync(int id)
        {
            return await _context.Campaigns
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == id);
        }

        public async Task<Campaign?> GetForUpdateAsync(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == id);
            
            // Ensure JobCampaigns is initialized
            if (campaign != null && campaign.JobCampaigns == null)
            {
                campaign.JobCampaigns = new List<JobCampaign>();
            }
            
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetByCompanyIdAsync(int companyId)
        {
            return await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId)
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Campaign>> GetByCompanyIdWithFiltersAsync(int companyId, int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)));
            }

            // Filter by status
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            // Filter by start date
            if (startDate.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDate.Value);
            }

            // Filter by end date
            if (endDate.HasValue)
            {
                query = query.Where(c => c.EndDate <= endDate.Value);
            }

            return await query
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalByCompanyIdWithFiltersAsync(int companyId, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)));
            }

            // Filter by status
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            // Filter by start date
            if (startDate.HasValue)
            {
                query = query.Where(c => c.StartDate >= startDate.Value);
            }

            // Filter by end date
            if (endDate.HasValue)
            {
                query = query.Where(c => c.EndDate <= endDate.Value);
            }

            return await query.CountAsync();
        }

        public async Task<List<JobCampaign>> GetActiveJobsByCampaignIdAsync(int campaignId)
        {
            return await _context.JobCampaigns
                .AsNoTracking()
                .Where(jc => jc.CampaignId == campaignId && jc.Job != null && jc.Job.IsActive)
                .Include(jc => jc.Job)
                .ToListAsync();
        }

        public async Task AddAsync(Campaign campaign)
        {
            await _context.Campaigns.AddAsync(campaign);
        }

        public void Update(Campaign campaign)
        {
            _context.Campaigns.Update(campaign);
        }

        // Legacy methods for backward compatibility
        public async Task UpdateAsync(Campaign campaign)
        {
            _context.Campaigns.Update(campaign);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Campaign campaign)
        {
            campaign.IsActive = false;
            _context.Campaigns.Update(campaign);
            await _context.SaveChangesAsync();
        }
    }
}

