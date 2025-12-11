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
        Task<List<ResumeApplication>> GetByJobIdResumeIdsAndCampaignAsync(int jobId, List<int> resumeIds, int campaignId);
        Task<List<ResumeApplication>> GetByJobIdAndCampaignWithResumeAsync(int jobId, int campaignId);
        Task<ResumeApplication?> GetByApplicationIdWithDetailsAsync(int applicationId);
        Task<List<ResumeApplication>> GetByResumeIdWithJobAsync(int resumeId);
        Task<ResumeApplication?> GetByResumeAndApplicationIdWithDetailsAsync(int resumeId, int applicationId);
        Task UpdateAsync(ResumeApplication resumeApplication);
        Task<bool> IsDuplicateResumeAsync(int jobId, int campaignId, string fileHash);
    }
}

