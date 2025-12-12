using Data.Entities;
using Data.Enum;
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

        public async Task<List<Campaign>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published)
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

            var campaigns = await query
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Filter out inactive jobs
            foreach (var campaign in campaigns)
            {
                if (campaign.JobCampaigns != null)
                {
                    campaign.JobCampaigns = campaign.JobCampaigns
                        .Where(jc => jc.Job != null && jc.Job.IsActive)
                        .ToList();
                }
            }
            
            return campaigns;
        }

        public async Task<int> GetTotalAsync(string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published)
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
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == id && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published);
            
            if (campaign != null && campaign.JobCampaigns != null)
            {
                campaign.JobCampaigns = campaign.JobCampaigns
                    .Where(jc => jc.Job != null && jc.Job.IsActive)
                    .ToList();
            }
            
            return campaign;
        }

        public async Task<Campaign?> GetForUpdateAsync(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == id && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published);
            
            // Ensure JobCampaigns is initialized
            if (campaign != null && campaign.JobCampaigns == null)
            {
                campaign.JobCampaigns = new List<JobCampaign>();
            }
            
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetByCompanyIdAsync(int companyId)
        {
            var campaigns = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published)
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            
            // Filter out inactive jobs
            foreach (var campaign in campaigns)
            {
                if (campaign.JobCampaigns != null)
                {
                    campaign.JobCampaigns = campaign.JobCampaigns
                        .Where(jc => jc.Job != null && jc.Job.IsActive)
                        .ToList();
                }
            }
            
            return campaigns;
        }

        public async Task<List<Campaign>> GetByCompanyIdWithFiltersAsync(int companyId, int page = 1, int pageSize = 10, string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published)
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

            var campaigns = await query
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Filter out inactive jobs
            foreach (var campaign in campaigns)
            {
                if (campaign.JobCampaigns != null)
                {
                    campaign.JobCampaigns = campaign.JobCampaigns
                        .Where(jc => jc.Job != null && jc.Job.IsActive)
                        .ToList();
                }
            }
            
            return campaigns;
        }

        public async Task<int> GetTotalByCompanyIdWithFiltersAsync(int companyId, string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId && c.Company.CompanyStatus == CompanyStatusEnum.Approved && c.Status == CampaignStatusEnum.Published)
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

        public async Task<int> MarkExpiredCampaignsAsync(DateTime currentDate, int? companyId = null)
        {
            var query = _context.Campaigns
                .Where(c => c.IsActive
                            && c.Status == CampaignStatusEnum.Published
                            && c.EndDate.Date <= currentDate.Date);

            if (companyId.HasValue)
            {
                query = query.Where(c => c.CompanyId == companyId.Value);
            }

            var expiredCampaigns = await query.ToListAsync();

            if (!expiredCampaigns.Any())
            {
                return 0;
            }

            foreach (var campaign in expiredCampaigns)
            {
                campaign.Status = CampaignStatusEnum.Expired;
            }

            return await _context.SaveChangesAsync();
        }

        public async Task<List<JobCampaign>> GetActiveJobsByCampaignIdAsync(int campaignId)
        {
            return await _context.JobCampaigns
                .AsNoTracking()
                .Where(jc => jc.CampaignId == campaignId && jc.Job != null && jc.Job.IsActive && jc.Job.Company.CompanyStatus == CompanyStatusEnum.Approved && jc.Job.JobStatus == JobStatusEnum.Published)
                .Include(jc => jc.Job)
                .Include(jc => jc.Job.Company)
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

        public async Task<List<Campaign>> GetPendingByCompanyIdAsync(int companyId, int page = 1, int pageSize = 10, string? search = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId && c.Status == CampaignStatusEnum.Pending)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)));
            }

            var campaigns = await query
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Filter out inactive jobs
            foreach (var campaign in campaigns)
            {
                if (campaign.JobCampaigns != null)
                {
                    campaign.JobCampaigns = campaign.JobCampaigns
                        .Where(jc => jc.Job != null && jc.Job.IsActive)
                        .ToList();
                }
            }
            
            return campaigns;
        }

        public async Task<int> GetTotalPendingByCompanyIdAsync(int companyId, string? search = null)
        {
            var query = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyId == companyId && c.Status == CampaignStatusEnum.Pending)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<Campaign?> GetPendingByIdAndCompanyIdAsync(int campaignId, int companyId)
        {
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(c => c.Creator)
                    .ThenInclude(u => u.Profile)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == campaignId && c.CompanyId == companyId && c.Status == CampaignStatusEnum.Pending);
            
            if (campaign != null && campaign.JobCampaigns != null)
            {
                campaign.JobCampaigns = campaign.JobCampaigns
                    .Where(jc => jc.Job != null && jc.Job.IsActive)
                    .ToList();
            }
            
            return campaign;
        }

        public async Task<Campaign?> GetForUpdateWithAllStatusesAsync(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Company)
                .Include(c => c.JobCampaigns)
                    .ThenInclude(jc => jc.Job)
                .FirstOrDefaultAsync(c => c.IsActive && c.CampaignId == id && c.Company.CompanyStatus == CompanyStatusEnum.Approved);
            
            if (campaign != null)
            {
                if (campaign.JobCampaigns == null)
                {
                    campaign.JobCampaigns = new List<JobCampaign>();
                }
                else
                {
                    // Filter out inactive jobs
                    campaign.JobCampaigns = campaign.JobCampaigns
                        .Where(jc => jc.Job != null && jc.Job.IsActive)
                        .ToList();
                }
            }
            
            return campaign;
        }

        public async Task<JobCampaign?> GetJobCampaignByJobIdAndCampaignIdAsync(int jobId, int campaignId)
        {
            return await _context.JobCampaigns
                .AsNoTracking()
                .Where(jc => jc.JobId == jobId && jc.CampaignId == campaignId)
                .FirstOrDefaultAsync();
        }
    }
}

