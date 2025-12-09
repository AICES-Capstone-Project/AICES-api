using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class CandidateRepository : ICandidateRepository
    {
        private readonly AICESDbContext _context;

        public CandidateRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Candidate> CreateAsync(Candidate candidate)
        {
            await _context.Candidates.AddAsync(candidate);
            return candidate;
        }

        public async Task<Candidate?> GetByResumeIdAsync(int resumeId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsActive && c.Resumes.Any(r => r.ResumeId == resumeId));
        }

        public async Task UpdateAsync(Candidate candidate)
        {
            _context.Candidates.Update(candidate);
        }

        public async Task<List<Candidate>> GetCandidatesWithScoresByJobIdAsync(int jobId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .Where(c => c.JobId == jobId && c.IsActive)
                .Include(c => c.Resumes.Where(r => r.IsActive))
                    .ThenInclude(r => r.ScoreDetails)
                .ToListAsync();
        }

        public async Task<List<Candidate>> GetCandidatesWithFullDetailsByJobIdAsync(int jobId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .Where(c => c.JobId == jobId && c.IsActive)
                .Include(c => c.Resumes.Where(r => r.IsActive))
                    .ThenInclude(r => r.ScoreDetails)
                        .ThenInclude(sd => sd.Criteria)
                .ToListAsync();
        }
    }
}
