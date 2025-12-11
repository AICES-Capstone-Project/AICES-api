using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IResumeRepository
    {
        Task<Resume> CreateAsync(Resume resume);
        Task<Resume?> GetByQueueJobIdAsync(string queueJobId);
        Task<Resume?> GetByIdAsync(int resumeId);
        Task<Resume?> GetForUpdateAsync(int resumeId);
        Task<Resume?> GetByIdWithDetailsAsync(int resumeId);
        Task UpdateAsync(Resume resume);
        Task<List<Resume>> GetByJobIdAsync(int jobId);
        Task<Resume?> GetByJobIdAndResumeIdAsync(int jobId, int resumeId);
        Task<List<Resume>> GetByCandidateIdAsync(int candidateId);
        Task<List<Resume>> GetPendingBeforeAsync(DateTime cutoff);
        Task<int> CountResumesInLastHoursAsync(int companyId, int hours);
        Task<int> CountResumesInLastHoursInTransactionAsync(int companyId, int hours);
        Task<int> CountResumesSinceDateAsync(int companyId, DateTime startDate, int hours);
        Task<int> CountResumesSinceDateInTransactionAsync(int companyId, DateTime startDate, int hours);
    }
}
