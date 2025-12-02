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

        public async Task<AIScores> AddAsync(AIScores aiScore)
        {
            await _context.AIScores.AddAsync(aiScore);
            return aiScore;
        }

        public async Task<AIScores?> GetByIdAsync(int scoreId)
        {
            return await _context.AIScores
                .AsNoTracking()
                .Include(ais => ais.AIScoreDetails!)
                    .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync(ais => ais.ScoreId == scoreId);
        }

        public async Task<AIScores?> GetByResumeIdAsync(int resumeId)
        {
            var candidate = await _context.ParsedCandidates
                .AsNoTracking()
                .Include(pc => pc.AIScores)
                    .ThenInclude(ais => ais.AIScoreDetails!)
                        .ThenInclude(aisd => aisd.Criteria)
                .FirstOrDefaultAsync(pc => pc.ResumeId == resumeId);

            return candidate?.AIScores?.OrderByDescending(ais => ais.CreatedAt).FirstOrDefault();
        }
    }
}

