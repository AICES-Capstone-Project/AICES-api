using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly AICESDbContext _context;

        public CategoryRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Category>> GetCategoriesAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCategoriesAsync(string? search = null)
        {
            var query = _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search));
            }

            return await query.CountAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == id);
        }

        public async Task AddAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
        }

        public void Update(Category category)
        {
            _context.Categories.Update(category);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Name == name);
        }

        public async Task<bool> ExistsAsync(int categoryId)
        {
            return await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.CategoryId == categoryId && c.IsActive);
        }

        // Legacy method for backward compatibility
        public async Task UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }
    }
}
