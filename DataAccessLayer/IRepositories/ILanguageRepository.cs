using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ILanguageRepository
    {
        Task<List<Language>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<int> GetTotalAsync(string? search = null);
        Task<Language?> GetByIdAsync(int id);
        Task<Language?> GetForUpdateAsync(int id);
        Task AddAsync(Language language);
        void Update(Language language);
        Task<bool> ExistsByNameAsync(string name);
        
        // Legacy methods for backward compatibility
        Task UpdateAsync(Language language);
        Task SoftDeleteAsync(Language language);
    }
}

