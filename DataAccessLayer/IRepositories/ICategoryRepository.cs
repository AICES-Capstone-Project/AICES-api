using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<List<Category>> GetCategoriesAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalCategoriesAsync(string? search = null);
        Task<Category?> GetByIdAsync(int id);
        Task<Category> AddAsync(Category category);
        Task UpdateAsync(Category category);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsAsync(int categoryId);
    }
}
