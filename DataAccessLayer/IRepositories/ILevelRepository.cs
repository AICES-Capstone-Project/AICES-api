using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ILevelRepository
    {
        Task<List<Level>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<int> GetTotalAsync(string? search = null);
        Task<Level?> GetByIdAsync(int id);
        Task<Level?> GetForUpdateAsync(int id);
        Task AddAsync(Level level);
        void Update(Level level);
        Task<bool> ExistsByNameAsync(string name);
        
        // Legacy methods for backward compatibility
        Task UpdateAsync(Level level);
        Task SoftDeleteAsync(Level level);
    }
}
