using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class SpecializationRepository : ISpecializationRepository
    {
        private readonly AICESDbContext _context;

        public SpecializationRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Specialization>> GetAllAsync()
        {
            return await _context.Specializations
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<Specialization?> GetByIdAsync(int id)
        {
            return await _context.Specializations
                .AsNoTracking()
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.SpecializationId == id);
        }

        public async Task<Specialization?> GetForUpdateAsync(int id)
        {
            return await _context.Specializations
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.SpecializationId == id);
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Specializations
                .AsNoTracking()
                .AnyAsync(s => s.SpecializationId == id && s.IsActive);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Specializations
                .AsNoTracking()
                .AnyAsync(s => s.Name == name && s.IsActive);
        }

        public async Task AddAsync(Specialization specialization)
        {
            await _context.Specializations.AddAsync(specialization);
        }

        public void Update(Specialization specialization)
        {
            _context.Specializations.Update(specialization);
        }
        
        // Legacy method for backward compatibility
        public async Task UpdateAsync(Specialization specialization)
        {
            _context.Specializations.Update(specialization);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Specialization>> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Specializations
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => s.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search));
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? search = null)
        {
            var query = _context.Specializations
                .AsNoTracking()
                .Where(s => s.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search));
            }

            return await query.CountAsync();
        }

        public async Task<List<Specialization>> GetByCategoryIdAsync(int categoryId)
        {
            return await _context.Specializations
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => s.CategoryId == categoryId && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
    }
}


