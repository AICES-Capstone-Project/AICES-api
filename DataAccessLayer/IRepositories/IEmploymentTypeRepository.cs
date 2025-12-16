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
        Task<(IEnumerable<EmploymentType> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<EmploymentType?> GetByIdAsync(int id);
        Task<EmploymentType?> GetForUpdateAsync(int id);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsAsync(int employmentTypeId);
        Task AddAsync(EmploymentType employmentType);
        void Update(EmploymentType employmentType);
        
        // Legacy method for backward compatibility
        Task UpdateAsync(EmploymentType employmentType);
    }
}
