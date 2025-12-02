using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IAIScoreDetailRepository
    {
        Task<AIScoreDetail> AddAsync(AIScoreDetail aiScoreDetail);
        Task<List<AIScoreDetail>> AddRangeAsync(List<AIScoreDetail> aiScoreDetails);
        Task<List<AIScoreDetail>> GetByScoreIdAsync(int scoreId);
    }
}

