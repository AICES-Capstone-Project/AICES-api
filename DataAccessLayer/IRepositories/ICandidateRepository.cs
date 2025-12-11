using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface ICandidateRepository
    {
        Task<Candidate> CreateAsync(Candidate candidate);
        Task UpdateAsync(Candidate candidate);
        Task<Candidate?> GetByResumeIdAsync(int resumeId);
        Task<Candidate?> GetByIdAsync(int id);
        Task<List<Candidate>> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalAsync(string? search = null);
        Task SoftDeleteAsync(Candidate candidate);
        Task<List<Candidate>> GetCandidatesWithScoresByJobIdAsync(int jobId);
        Task<List<Candidate>> GetCandidatesWithFullDetailsByJobIdAsync(int jobId);
    }
}
