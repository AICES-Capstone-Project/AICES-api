using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IParsedResumeRepository
    {
        Task<ParsedResumes> CreateAsync(ParsedResumes parsedResume);
        Task<ParsedResumes?> GetByQueueJobIdAsync(string queueJobId);
        Task<ParsedResumes?> GetByIdAsync(int resumeId);
        Task<ParsedResumes?> GetForUpdateAsync(int resumeId);
        Task<ParsedResumes?> GetByIdWithDetailsAsync(int resumeId);
        Task UpdateAsync(ParsedResumes parsedResume);
        Task<List<ParsedResumes>> GetByJobIdAsync(int jobId);
        Task<ParsedResumes?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId);
        Task<List<ParsedResumes>> GetPendingBeforeAsync(DateTime cutoff);
        Task<int> CountResumesInLastHoursAsync(int companyId, int hours);
        Task<int> CountResumesInLastHoursInTransactionAsync(int companyId, int hours);
    }
}

