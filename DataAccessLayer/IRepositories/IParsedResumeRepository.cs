using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IParsedResumeRepository
    {
        Task<ParsedResumes> CreateAsync(ParsedResumes parsedResume);
        Task<ParsedResumes?> GetByQueueJobIdAsync(string queueJobId);
        Task<ParsedResumes?> GetByIdAsync(int resumeId);
        Task<ParsedResumes?> GetByIdWithDetailsAsync(int resumeId);
        Task UpdateAsync(ParsedResumes parsedResume);
    }
}

