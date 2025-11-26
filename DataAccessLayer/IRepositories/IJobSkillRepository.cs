using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobSkillRepository
    {
        Task<IEnumerable<JobSkill>> GetAllAsync();
        Task<JobSkill?> GetByIdAsync(int id);
        Task<JobSkill?> GetForUpdateAsync(int id);
        Task AddAsync(JobSkill jobSkill);
        void Update(JobSkill jobSkill);
        void Delete(JobSkill jobSkill);
        Task<List<JobSkill>> GetByJobIdAsync(int jobId);
        Task AddRangeAsync(List<JobSkill> jobSkills);
        Task DeleteByJobIdAsync(int jobId);
        
        // Legacy methods for backward compatibility (used by JobSkillService)
        // These include SaveChanges for services not using UoW
        Task UpdateAsync(JobSkill jobSkill);
        Task DeleteAsync(JobSkill jobSkill);
    }
}
