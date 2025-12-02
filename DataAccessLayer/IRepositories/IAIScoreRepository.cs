using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IAIScoreRepository
    {
        Task<AIScores> AddAsync(AIScores aiScore);
        Task<AIScores?> GetByIdAsync(int scoreId);
        Task<AIScores?> GetByResumeIdAsync(int resumeId);
    }
}

