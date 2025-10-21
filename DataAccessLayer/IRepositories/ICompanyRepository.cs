using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyRepository
    {
        Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false);

        Task<Company?> GetByIdAsync(int id);
        Task<Company> AddAsync(Company company);
        Task UpdateAsync(Company company);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsAsync(int companyId);
        Task<bool> UpdateUserRoleByCompanyAsync(int companyId, string newRoleName);

    }
}
