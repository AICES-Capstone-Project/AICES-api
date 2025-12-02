using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IParsedCandidateRepository
    {
        Task<ParsedCandidates> AddAsync(ParsedCandidates parsedCandidate);
        Task UpdateAsync(ParsedCandidates parsedCandidate);
        Task<ParsedCandidates?> GetByResumeIdAsync(int resumeId);
    }
}

