using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IEmploymentTypeRepository
    {
        Task<IEnumerable<EmploymentType>> GetAllAsync();
        Task<EmploymentType?> GetByIdAsync(int id);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsAsync(int employmentTypeId);
        Task<EmploymentType> AddAsync(EmploymentType employmentType);
        Task UpdateAsync(EmploymentType employmentType);
    }
}
