using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ResumeRepository : IResumeRepository
    {
        private readonly AICESDbContext _context;

        public ResumeRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Resume> CreateAsync(Resume resume)
        {
            await _context.Resumes.AddAsync(resume);
            return resume;
        }

        public async Task<Resume?> GetByQueueJobIdAsync(string queueJobId)
        {
            return await _context.Resumes
                .FirstOrDefaultAsync(r => r.QueueJobId == queueJobId);
        }

        public async Task<Resume?> GetByIdAsync(int resumeId)
        {
            return await _context.Resumes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ResumeId == resumeId);
        }

        public async Task<Resume?> GetForUpdateAsync(int resumeId)
        {
            return await _context.Resumes
                .FirstOrDefaultAsync(r => r.ResumeId == resumeId);
        }

        public async Task<Resume?> GetByIdWithDetailsAsync(int resumeId)
        {
            var resume = await _context.Resumes
                .AsNoTracking()
                .Include(r => r.Candidate)
                .FirstOrDefaultAsync(r => r.ResumeId == resumeId);

            // Note: ScoreDetails are now on ResumeApplication, not Resume
            // Access them through ResumeApplication if needed
            return resume;
        }

        public async Task UpdateAsync(Resume resume)
        {
            _context.Resumes.Update(resume);
        }

        public async Task<List<Resume>> GetByJobIdAsync(int jobId)
        {
            // Include resumes and their candidates before projection to avoid Include-after-Select errors
            return await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId && ra.IsActive)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .Select(ra => ra.Resume)
                .Where(r => r != null && r.IsActive)
                .Distinct()
                .OrderByDescending(r => r!.CreatedAt)
                .ToListAsync();
        }

        public async Task<Resume?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId)
        {
            var application = await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.JobId == jobId && ra.ResumeId == resumeId && ra.IsActive)
                .Include(ra => ra.Resume)
                    .ThenInclude(r => r.Candidate)
                .Include(ra => ra.ScoreDetails)
                    .ThenInclude(sd => sd.Criteria)
                .FirstOrDefaultAsync();

            if (application?.Resume == null)
                return null;

            // Note: ScoreDetails are now on ResumeApplication, not Resume
            // The Resume object returned here won't have ScoreDetails directly
            // Access them through ResumeApplication if needed
            return application.Resume;
        }

        public async Task<List<Resume>> GetByCandidateIdAsync(int candidateId)
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && r.CandidateId == candidateId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Resume>> GetPendingBeforeAsync(DateTime cutoff)
        {
            return await _context.Resumes
                .AsNoTracking()
                .Where(x => x.Status == ResumeStatusEnum.Pending 
                         && x.CreatedAt < cutoff
                         && x.IsActive)
                .ToListAsync();
        }

        public async Task<int> CountResumesInLastHoursAsync(int companyId, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.CompanyId == companyId
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= hoursAgo)
                .CountAsync();
        }

        public async Task<int> CountResumesInLastHoursInTransactionAsync(int companyId, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            // No AsNoTracking() to see records created in current transaction
            return await _context.Resumes
                .Where(r => r.CompanyId == companyId
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= hoursAgo)
                .CountAsync();
        }

        public async Task<int> CountResumesSinceDateAsync(int companyId, DateTime startDate, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            var effectiveStartDate = startDate > hoursAgo ? startDate : hoursAgo;
            
            return await _context.Resumes
                .AsNoTracking()
                .Where(r => r.CompanyId == companyId
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= effectiveStartDate)
                .CountAsync();
        }

        public async Task<int> CountResumesSinceDateInTransactionAsync(int companyId, DateTime startDate, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            var effectiveStartDate = startDate > hoursAgo ? startDate : hoursAgo;
            
            // No AsNoTracking() to see records created in current transaction
            return await _context.Resumes
                .Where(r => r.CompanyId == companyId
                    && r.CreatedAt.HasValue
                    && r.CreatedAt.Value >= effectiveStartDate)
                .CountAsync();
        }
    }
}
