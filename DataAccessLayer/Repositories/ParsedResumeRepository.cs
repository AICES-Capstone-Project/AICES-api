using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ParsedResumeRepository : IParsedResumeRepository
    {
        private readonly AICESDbContext _context;

        public ParsedResumeRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<ParsedResumes> CreateAsync(ParsedResumes parsedResume)
        {
            await _context.ParsedResumes.AddAsync(parsedResume);
            return parsedResume;
        }

        public async Task<ParsedResumes?> GetByQueueJobIdAsync(string queueJobId)
        {
            return await _context.ParsedResumes
                .FirstOrDefaultAsync(pr => pr.QueueJobId == queueJobId);
        }

        public async Task<ParsedResumes?> GetByIdAsync(int resumeId)
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.ResumeId == resumeId);
        }

        public async Task<ParsedResumes?> GetForUpdateAsync(int resumeId)
        {
            return await _context.ParsedResumes
                .FirstOrDefaultAsync(pr => pr.ResumeId == resumeId);
        }

        public async Task<ParsedResumes?> GetByIdWithDetailsAsync(int resumeId)
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                        .ThenInclude(ais => ais!.AIScoreDetails)
                            .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync(pr => pr.ResumeId == resumeId);
        }

        public async Task UpdateAsync(ParsedResumes parsedResume)
        {
            _context.ParsedResumes.Update(parsedResume);
        }

        public async Task<List<ParsedResumes>> GetByJobIdAsync(int jobId)
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                .Where(pr => pr.JobId == jobId && pr.IsActive)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync();
        }

        public async Task<ParsedResumes?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId)
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                        .ThenInclude(ais => ais!.AIScoreDetails)
                            .ThenInclude(aisd => aisd.Criteria)
                .Where(pr => pr.JobId == jobId && pr.ResumeId == resumeId && pr.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ParsedResumes>> GetPendingBeforeAsync(DateTime cutoff)
        {
            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(x => x.ResumeStatus == ResumeStatusEnum.Pending 
                         && x.CreatedAt < cutoff
                         && x.IsActive)
                .ToListAsync();
        }

        public async Task<int> CountResumesInLastHoursAsync(int companyId, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(pr => pr.CompanyId == companyId
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= hoursAgo)
                .CountAsync();
        }

        public async Task<int> CountResumesInLastHoursInTransactionAsync(int companyId, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            // No AsNoTracking() to see records created in current transaction
            return await _context.ParsedResumes
                .Where(pr => pr.CompanyId == companyId
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= hoursAgo)
                .CountAsync();
        }

        public async Task<int> CountResumesSinceDateAsync(int companyId, DateTime startDate, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            var effectiveStartDate = startDate > hoursAgo ? startDate : hoursAgo;
            
            return await _context.ParsedResumes
                .AsNoTracking()
                .Where(pr => pr.CompanyId == companyId
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= effectiveStartDate)
                .CountAsync();
        }

        public async Task<int> CountResumesSinceDateInTransactionAsync(int companyId, DateTime startDate, int hours)
        {
            var hoursAgo = DateTime.UtcNow.AddHours(-hours);
            var effectiveStartDate = startDate > hoursAgo ? startDate : hoursAgo;
            
            // No AsNoTracking() to see records created in current transaction
            return await _context.ParsedResumes
                .Where(pr => pr.CompanyId == companyId
                    && pr.CreatedAt.HasValue
                    && pr.CreatedAt.Value >= effectiveStartDate)
                .CountAsync();
        }
    }
}

