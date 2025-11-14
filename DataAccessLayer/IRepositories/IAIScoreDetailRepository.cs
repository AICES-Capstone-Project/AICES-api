using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IAIScoreDetailRepository
    {
        Task<AIScoreDetail> CreateAsync(AIScoreDetail aiScoreDetail);
        Task<List<AIScoreDetail>> CreateRangeAsync(List<AIScoreDetail> aiScoreDetails);
        Task<List<AIScoreDetail>> GetByScoreIdAsync(int scoreId);
    }
}

