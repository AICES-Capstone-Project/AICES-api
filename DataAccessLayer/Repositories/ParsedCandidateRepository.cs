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
    }
}

