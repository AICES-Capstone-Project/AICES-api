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

        public async Task<AIScoreDetail> AddAsync(AIScoreDetail aiScoreDetail)
        {
            await _context.AIScoreDetails.AddAsync(aiScoreDetail);
            return aiScoreDetail;
        }

        public async Task<List<AIScoreDetail>> AddRangeAsync(List<AIScoreDetail> aiScoreDetails)
        {
            await _context.AIScoreDetails.AddRangeAsync(aiScoreDetails);
            return aiScoreDetails;
        }

        public async Task<List<AIScoreDetail>> GetByScoreIdAsync(int scoreId)
        {
            return await _context.AIScoreDetails
                .AsNoTracking()
                .Include(aisd => aisd.Criteria)
                .Where(aisd => aisd.ScoreId == scoreId)
                .ToListAsync();
        }
    }
}

