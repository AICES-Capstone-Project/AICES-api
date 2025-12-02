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
        Task<Skill?> GetByIdForUpdateAsync(int id);
        Task AddAsync(Skill skill);
        Task UpdateAsync(Skill skill);
        Task<bool> ExistsByNameAsync(string name);
        Task SoftDeleteAsync(Skill skill);
    }
}
