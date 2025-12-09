using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface ICandidateRepository
    {
        Task<Candidate> CreateAsync(Candidate candidate);
        Task UpdateAsync(Candidate candidate);
        Task<Candidate?> GetByResumeIdAsync(int resumeId);
        Task<List<Candidate>> GetCandidatesWithScoresByJobIdAsync(int jobId);
        Task<List<Candidate>> GetCandidatesWithFullDetailsByJobIdAsync(int jobId);
    }
}
