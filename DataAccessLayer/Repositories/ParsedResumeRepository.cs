using Data.Entities;
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
            _context.ParsedResumes.Add(parsedResume);
            await _context.SaveChangesAsync();
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
                .FirstOrDefaultAsync(pr => pr.ResumeId == resumeId);
        }

        public async Task<ParsedResumes?> GetByIdWithDetailsAsync(int resumeId)
        {
            return await _context.ParsedResumes
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                        .ThenInclude(ais => ais!.AIScoreDetails)
                            .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync(pr => pr.ResumeId == resumeId);
        }

        public async Task UpdateAsync(ParsedResumes parsedResume)
        {
            _context.ParsedResumes.Update(parsedResume);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ParsedResumes>> GetByJobIdAsync(int jobId)
        {
            return await _context.ParsedResumes
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                .Where(pr => pr.JobId == jobId && pr.IsActive)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync();
        }

        public async Task<ParsedResumes?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId)
        {
            return await _context.ParsedResumes
                .Include(pr => pr.ParsedCandidates)
                    .ThenInclude(pc => pc!.AIScores)
                        .ThenInclude(ais => ais!.AIScoreDetails)
                            .ThenInclude(aisd => aisd.Criteria)
                .Where(pr => pr.JobId == jobId && pr.ResumeId == resumeId && pr.IsActive)
                .FirstOrDefaultAsync();
        }
    }
}

