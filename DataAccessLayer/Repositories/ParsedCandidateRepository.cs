using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ParsedCandidateRepository : IParsedCandidateRepository
    {
        private readonly AICESDbContext _context;

        public ParsedCandidateRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<ParsedCandidates> CreateAsync(ParsedCandidates parsedCandidate)
        {
            await _context.ParsedCandidates.AddAsync(parsedCandidate);
            return parsedCandidate;
        }

        public async Task<ParsedCandidates?> GetByResumeIdAsync(int resumeId)
        {
            return await _context.ParsedCandidates
                .AsNoTracking()
                .FirstOrDefaultAsync(pc => pc.ResumeId == resumeId);
        }

        public async Task UpdateAsync(ParsedCandidates parsedCandidate)
        {
            _context.ParsedCandidates.Update(parsedCandidate);
        }

        public async Task<List<ParsedCandidates>> GetCandidatesWithScoresByJobIdAsync(int jobId)
        {
            return await _context.ParsedCandidates
                .AsNoTracking()
                .Where(pc => pc.JobId == jobId && pc.ParsedResumes.IsActive)
                .Include(pc => pc.AIScores)
                .Include(pc => pc.RankingResult)
                .ToListAsync();
        }

        public async Task<List<ParsedCandidates>> GetCandidatesWithFullDetailsByJobIdAsync(int jobId)
        {
            return await _context.ParsedCandidates
                .AsNoTracking()
                .Where(pc => pc.JobId == jobId && pc.ParsedResumes.IsActive)
                .Include(pc => pc.AIScores)
                    .ThenInclude(ais => ais.AIScoreDetails!)
                        .ThenInclude(asd => asd.Criteria)
                .Include(pc => pc.RankingResult)
                .Include(pc => pc.ParsedResumes)
                .ToListAsync();
        }
    }
}

