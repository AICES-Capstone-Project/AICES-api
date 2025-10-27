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
        Task<JobSkill> AddAsync(JobSkill jobSkill);
        Task UpdateAsync(JobSkill jobSkill);
        Task DeleteAsync(JobSkill jobSkill);
    }
}
