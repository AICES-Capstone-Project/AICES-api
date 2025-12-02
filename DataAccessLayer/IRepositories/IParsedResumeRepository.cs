using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IParsedResumeRepository
    {
        Task<ParsedResumes> AddAsync(ParsedResumes parsedResume);
        Task<ParsedResumes?> GetByQueueJobIdAsync(string queueJobId);
        Task<ParsedResumes?> GetByIdAsync(int resumeId);
        Task<ParsedResumes?> GetByIdForUpdateAsync(int resumeId);
        Task<ParsedResumes?> GetByIdWithDetailsAsync(int resumeId);
        Task UpdateAsync(ParsedResumes parsedResume);
        Task<List<ParsedResumes>> GetByJobIdAsync(int jobId);
        Task<ParsedResumes?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId);
        Task<List<ParsedResumes>> GetPendingBeforeAsync(DateTime cutoff);
        Task<int> CountByCompanyIdSinceAsync(int companyId, int hours);
        Task<int> CountByCompanyIdSinceInTransactionAsync(int companyId, int hours);
        Task<int> CountByCompanyIdSinceDateAsync(int companyId, DateTime startDate, int hours);
        Task<int> CountByCompanyIdSinceDateInTransactionAsync(int companyId, DateTime startDate, int hours);
    }
}

