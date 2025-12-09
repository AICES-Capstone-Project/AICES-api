using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IScoreDetailRepository
    {
        Task<ScoreDetail> CreateAsync(ScoreDetail scoreDetail);
        Task<List<ScoreDetail>> CreateRangeAsync(List<ScoreDetail> scoreDetails);
        Task<List<ScoreDetail>> GetByResumeIdAsync(int resumeId);
    }
}
