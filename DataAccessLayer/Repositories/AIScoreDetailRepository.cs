using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class AIScoreDetailRepository : IAIScoreDetailRepository
    {
        private readonly AICESDbContext _context;

        public AIScoreDetailRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<AIScoreDetail> CreateAsync(AIScoreDetail aiScoreDetail)
        {
            _context.AIScoreDetails.Add(aiScoreDetail);
            await _context.SaveChangesAsync();
            return aiScoreDetail;
        }

        public async Task<List<AIScoreDetail>> CreateRangeAsync(List<AIScoreDetail> aiScoreDetails)
        {
            _context.AIScoreDetails.AddRange(aiScoreDetails);
            await _context.SaveChangesAsync();
            return aiScoreDetails;
        }

        public async Task<List<AIScoreDetail>> GetByScoreIdAsync(int scoreId)
        {
            return await _context.AIScoreDetails
                .Include(aisd => aisd.Criteria)
                .Where(aisd => aisd.ScoreId == scoreId)
                .ToListAsync();
        }
    }
}

