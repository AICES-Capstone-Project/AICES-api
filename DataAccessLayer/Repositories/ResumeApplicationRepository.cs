using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ResumeApplicationRepository : IResumeApplicationRepository
    {
        private readonly AICESDbContext _context;

        public ResumeApplicationRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<ResumeApplication> CreateAsync(ResumeApplication resumeApplication)
        {
            await _context.ResumeApplications.AddAsync(resumeApplication);
            return resumeApplication;
        }

        public async Task<ResumeApplication?> GetByResumeIdAsync(int resumeId)
        {
            return await _context.ResumeApplications
                .Where(ra => ra.ResumeId == resumeId && ra.IsActive)
                .OrderByDescending(ra => ra.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ResumeApplication?> GetByResumeIdAndJobIdAsync(int resumeId, int jobId)
        {
            return await _context.ResumeApplications
                .Where(ra => ra.ResumeId == resumeId && ra.JobId == jobId && ra.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<ResumeApplication?> GetByResumeIdAndJobIdWithDetailsAsync(int resumeId, int jobId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.JobId == jobId && ra.IsActive)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ResumeApplication>> GetByJobIdAndResumeIdsAsync(int jobId, List<int> resumeIds)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId && resumeIds.Contains(ra.ResumeId) && ra.IsActive)
                .ToListAsync();
        }

        public async Task<List<ResumeApplication>> GetByJobIdResumeIdsAndCampaignAsync(int jobId, List<int> resumeIds, int campaignId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId
                             && ra.CampaignId == campaignId
                             && resumeIds.Contains(ra.ResumeId)
                             && ra.IsActive)
                .ToListAsync();
        }

        public async Task<List<ResumeApplication>> GetByJobIdAndApplicationIdsAndCampaignAsync(int jobId, List<int> applicationIds, int campaignId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId
                             && ra.CampaignId == campaignId
                             && applicationIds.Contains(ra.ApplicationId)
                             && ra.IsActive)
                .ToListAsync();
        }

        public async Task<List<ResumeApplication>> GetByJobIdAndCampaignWithResumeAsync(int jobId, int campaignId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId
                             && ra.CampaignId == campaignId
                             && ra.IsActive)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .ToListAsync();
        }

        public async Task<(List<ResumeApplication> applications, int totalCount)> GetByJobIdAndCampaignWithResumePagedAsync(
            int jobId, int campaignId, int page, int pageSize, string? search,
            decimal? minScore, decimal? maxScore, Data.Enum.ApplicationStatusEnum? status)
        {
            var query = _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId
                             && ra.CampaignId == campaignId
                             && ra.IsActive)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .AsQueryable();

            // Search filter (name, email, phone)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(ra =>
                    (ra.Resume.Candidate != null && ra.Resume.Candidate.FullName != null && ra.Resume.Candidate.FullName.ToLower().Contains(searchLower)) ||
                    (ra.Resume.Candidate != null && ra.Resume.Candidate.Email != null && ra.Resume.Candidate.Email.ToLower().Contains(searchLower)) ||
                    (ra.Resume.Candidate != null && ra.Resume.Candidate.PhoneNumber != null && ra.Resume.Candidate.PhoneNumber.ToLower().Contains(searchLower))
                );
            }

            // Score filter
            if (minScore.HasValue)
            {
                query = query.Where(ra => ra.AdjustedScore.HasValue && ra.AdjustedScore >= minScore || 
                                         !ra.AdjustedScore.HasValue && ra.TotalScore.HasValue && ra.TotalScore >= minScore);
            }
            if (maxScore.HasValue)
            {
                query = query.Where(ra => ra.AdjustedScore.HasValue && ra.AdjustedScore <= maxScore || 
                                         !ra.AdjustedScore.HasValue && ra.TotalScore.HasValue && ra.TotalScore <= maxScore);
            }

            // Status filter
            if (status.HasValue)
            {
                query = query.Where(ra => ra.Status == status.Value);
            }

            var totalCount = await query.CountAsync();

            var applications = await query
                .OrderByDescending(ra => ra.AdjustedScore ?? ra.TotalScore ?? 0)
                .ThenByDescending(ra => ra.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (applications, totalCount);
        }

        public async Task<ResumeApplication?> GetByApplicationIdWithDetailsAsync(int applicationId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ApplicationId == applicationId && ra.IsActive)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ResumeApplication>> GetByResumeIdWithJobAsync(int resumeId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.IsActive)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .ToListAsync();
        }

        public async Task<ResumeApplication?> GetByResumeAndApplicationIdWithDetailsAsync(int resumeId, int applicationId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.ApplicationId == applicationId && ra.IsActive)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();
        }

        public async Task<ResumeApplication?> GetByQueueJobIdWithDetailsAsync(string queueJobId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.QueueJobId == queueJobId && ra.IsActive)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(ResumeApplication resumeApplication)
        {
            _context.ResumeApplications.Update(resumeApplication);
        }

        public async Task<bool> IsDuplicateResumeAsync(int jobId, int campaignId, string fileHash)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId
                    && ra.CampaignId == campaignId
                    && ra.Resume.FileHash == fileHash
                    && (ra.Resume.Status == Data.Enum.ResumeStatusEnum.Completed || ra.Resume.Status == Data.Enum.ResumeStatusEnum.Pending)
                    && ra.Resume.IsActive == true
                    && ra.IsActive == true)
                .Include(ra => ra.Resume)
                .AnyAsync();
        }

        public async Task<List<ResumeApplication>> GetByResumeIdWithJobAndCompanyAsync(int resumeId, int companyId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.IsActive && ra.Job.CompanyId == companyId)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .ToListAsync();
        }

        public async Task<(List<ResumeApplication> applications, int totalCount)> GetByResumeIdWithJobAndCompanyPagedAsync(
            int resumeId, int companyId, int page, int pageSize, string? search,
            decimal? minScore, decimal? maxScore, Data.Enum.ApplicationStatusEnum? status)
        {
            var query = _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.IsActive && ra.Job.CompanyId == companyId)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .AsQueryable();

            // Search filter (job title, company name, campaign title)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(ra =>
                    (ra.Job != null && ra.Job.Title != null && ra.Job.Title.ToLower().Contains(searchLower)) ||
                    (ra.Job != null && ra.Job.Company != null && ra.Job.Company.Name != null && ra.Job.Company.Name.ToLower().Contains(searchLower)) ||
                    (ra.Campaign != null && ra.Campaign.Title != null && ra.Campaign.Title.ToLower().Contains(searchLower))
                );
            }

            // Score filter
            if (minScore.HasValue)
            {
                query = query.Where(ra => ra.AdjustedScore.HasValue && ra.AdjustedScore >= minScore || 
                                         !ra.AdjustedScore.HasValue && ra.TotalScore.HasValue && ra.TotalScore >= minScore);
            }
            if (maxScore.HasValue)
            {
                query = query.Where(ra => ra.AdjustedScore.HasValue && ra.AdjustedScore <= maxScore || 
                                         !ra.AdjustedScore.HasValue && ra.TotalScore.HasValue && ra.TotalScore <= maxScore);
            }

            // Status filter
            if (status.HasValue)
            {
                query = query.Where(ra => ra.Status == status.Value);
            }

            var totalCount = await query.CountAsync();

            var applications = await query
                .OrderByDescending(ra => ra.AdjustedScore ?? ra.TotalScore ?? 0)
                .ThenByDescending(ra => ra.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (applications, totalCount);
        }

        public async Task<ResumeApplication?> GetByResumeAndApplicationIdWithDetailsAndCompanyAsync(int resumeId, int applicationId, int companyId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ResumeId == resumeId && ra.ApplicationId == applicationId && ra.IsActive && ra.Job.CompanyId == companyId)
                .Include(ra => ra.Job)
                    .ThenInclude(j => j.Company)
                .Include(ra => ra.Campaign)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();
        }

        public async Task<ResumeApplication?> GetApplicationByIdAndCompanyAsync(int applicationId, int companyId)
        {
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.ApplicationId == applicationId && ra.IsActive && ra.Job.CompanyId == companyId)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .Include(ra => ra.Job)
                .Include(ra => ra.Campaign)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();
        }
    }
}

