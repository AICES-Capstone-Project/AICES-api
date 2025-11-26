using Data.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ISpecializationRepository
    {
        Task<IEnumerable<Specialization>> GetAllAsync();
        Task<Specialization?> GetByIdAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name);
        Task AddAsync(Specialization specialization);
        void Update(Specialization specialization);
        Task<List<Specialization>> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalCountAsync(string? search = null);
        Task<List<Specialization>> GetByCategoryIdAsync(int categoryId);
        
        // Legacy method for backward compatibility
        Task UpdateAsync(Specialization specialization);
    }
}


