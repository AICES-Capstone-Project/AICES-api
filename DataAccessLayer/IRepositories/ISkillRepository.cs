using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ISkillRepository
    {
        Task<IEnumerable<Skill>> GetAllAsync();
        Task<Skill?> GetByIdAsync(int id);
        Task AddAsync(Skill skill);
        void Update(Skill skill);
        Task<bool> ExistsByNameAsync(string name);
        
        // Legacy methods for backward compatibility
        Task UpdateAsync(Skill skill);
        Task SoftDeleteAsync(Skill skill);
    }
}
