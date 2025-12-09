using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ScoreDetailRepository : IScoreDetailRepository
    {
        private readonly AICESDbContext _context;

        public ScoreDetailRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<ScoreDetail> CreateAsync(ScoreDetail scoreDetail)
        {
            await _context.ScoreDetails.AddAsync(scoreDetail);
            return scoreDetail;
        }

        public async Task<List<ScoreDetail>> CreateRangeAsync(List<ScoreDetail> scoreDetails)
        {
            await _context.ScoreDetails.AddRangeAsync(scoreDetails);
            return scoreDetails;
        }

        public async Task<List<ScoreDetail>> GetByResumeIdAsync(int resumeId)
        {
            return await _context.ScoreDetails
                .AsNoTracking()
                .Include(sd => sd.Criteria)
                .Where(sd => sd.IsActive && sd.ResumeId == resumeId)
                .ToListAsync();
        }
    }
}
