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

        public async Task UpdateAsync(ResumeApplication resumeApplication)
        {
            _context.ResumeApplications.Update(resumeApplication);
        }
    }
}

