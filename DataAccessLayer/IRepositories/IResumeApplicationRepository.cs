using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IResumeApplicationRepository
    {
        Task<ResumeApplication> CreateAsync(ResumeApplication resumeApplication);
        Task<ResumeApplication?> GetByResumeIdAsync(int resumeId);
        Task<ResumeApplication?> GetByResumeIdAndJobIdAsync(int resumeId, int jobId);
        Task<ResumeApplication?> GetByResumeIdAndJobIdWithDetailsAsync(int resumeId, int jobId);
        Task<List<ResumeApplication>> GetByJobIdAndResumeIdsAsync(int jobId, List<int> resumeIds);
        Task UpdateAsync(ResumeApplication resumeApplication);
    }
}

