using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IParsedCandidateRepository
    {
        Task<ParsedCandidates> CreateAsync(ParsedCandidates parsedCandidate);
        Task UpdateAsync(ParsedCandidates parsedCandidate);
        Task<ParsedCandidates?> GetByResumeIdAsync(int resumeId);
        Task<List<ParsedCandidates>> GetCandidatesWithScoresByJobIdAsync(int jobId);
    }
}

