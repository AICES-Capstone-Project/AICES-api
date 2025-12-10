using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobLanguageRepository
    {
        Task<IEnumerable<JobLanguage>> GetAllAsync();
        Task<JobLanguage?> GetByJobIdAndLanguageIdAsync(int jobId, int languageId);
        Task<JobLanguage?> GetForUpdateByJobIdAndLanguageIdAsync(int jobId, int languageId);
        Task AddAsync(JobLanguage jobLanguage);
        void Update(JobLanguage jobLanguage);
        void Delete(JobLanguage jobLanguage);
        Task<List<JobLanguage>> GetByJobIdAsync(int jobId);
        Task AddRangeAsync(List<JobLanguage> jobLanguages);
        Task DeleteByJobIdAsync(int jobId);
        
        // Legacy methods for backward compatibility
        Task UpdateAsync(JobLanguage jobLanguage);
        Task DeleteAsync(JobLanguage jobLanguage);
    }
}

