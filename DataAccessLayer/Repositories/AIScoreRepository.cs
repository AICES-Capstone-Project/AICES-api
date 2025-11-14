using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class AIScoreRepository : IAIScoreRepository
    {
        private readonly AICESDbContext _context;

        public AIScoreRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<AIScores> CreateAsync(AIScores aiScore)
        {
            _context.AIScores.Add(aiScore);
            await _context.SaveChangesAsync();
            return aiScore;
        }

        public async Task<AIScores?> GetByIdAsync(int scoreId)
        {
            return await _context.AIScores
                .Include(ais => ais.AIScoreDetails)
                    .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync(ais => ais.ScoreId == scoreId);
        }

        public async Task<AIScores?> GetByResumeIdAsync(int resumeId)
        {
            return await _context.ParsedCandidates
                .Where(pc => pc.ResumeId == resumeId)
                .Select(pc => pc.AIScores)
                .Include(ais => ais.AIScoreDetails)
                    .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync();
        }
    }
}

