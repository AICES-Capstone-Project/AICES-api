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
        Task<List<ResumeApplication>> GetByJobIdAndApplicationIdsAndCampaignAsync(int jobId, List<int> applicationIds, int campaignId);
        Task<List<ResumeApplication>> GetByJobIdAndCampaignWithResumeAsync(int jobId, int campaignId);
        Task<(List<ResumeApplication> applications, int totalCount)> GetByJobIdAndCampaignWithResumePagedAsync(
            int jobId, int campaignId, int page, int pageSize, string? search, 
            decimal? minScore, decimal? maxScore, Data.Enum.ApplicationStatusEnum? status, Data.Enum.ResumeSortByEnum sortBy, Data.Enum.ProcessingModeEnum? processingMode);
        Task<ResumeApplication?> GetByApplicationIdWithDetailsAsync(int applicationId);
        Task<List<ResumeApplication>> GetByResumeIdWithJobAsync(int resumeId);
        Task<(List<ResumeApplication> applications, int totalCount)> GetByResumeIdWithJobAndCompanyPagedAsync(
            int resumeId, int companyId, int page, int pageSize, string? search, 
            decimal? minScore, decimal? maxScore, Data.Enum.ApplicationStatusEnum? status, Data.Enum.ResumeSortByEnum sortBy, Data.Enum.ProcessingModeEnum? processingMode);
        Task<ResumeApplication?> GetByResumeAndApplicationIdWithDetailsAsync(int resumeId, int applicationId);
        Task<ResumeApplication?> GetByQueueJobIdWithDetailsAsync(string queueJobId);
        Task UpdateAsync(ResumeApplication resumeApplication);
        Task<bool> IsDuplicateResumeAsync(int jobId, int campaignId, string fileHash);
        Task<List<ResumeApplication>> GetByResumeIdWithJobAndCompanyAsync(int resumeId, int companyId);
        Task<ResumeApplication?> GetByResumeAndApplicationIdWithDetailsAndCompanyAsync(int resumeId, int applicationId, int companyId);
        Task<ResumeApplication?> GetApplicationByIdAndCompanyAsync(int applicationId, int companyId);
        Task<List<int>> GetJobIdsWithApplicationsInCampaignAsync(int campaignId, List<int> jobIds);
        Task<Dictionary<int, string>> GetJobTitlesWithApplicationsInCampaignAsync(int campaignId, List<int> jobIds);
        Task<List<ResumeApplication>> GetForExcelExportAsync(int jobId, int campaignId);
        Task<List<ResumeApplication>> GetForPdfExportAsync(int jobId, int campaignId);
    }
}

