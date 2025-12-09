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
    public class LevelRepository : ILevelRepository
    {
        private readonly AICESDbContext _context;

        public LevelRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<Level>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var query = _context.Levels
                .AsNoTracking()
                .Where(l => l.IsActive)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.Name.Contains(search));
            }

            return await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalAsync(string? search = null)
        {
            var query = _context.Levels
                .AsNoTracking()
                .Where(l => l.IsActive)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.Name.Contains(search));
            }

            return await query.CountAsync();
        }

        public async Task<Level?> GetByIdAsync(int id)
        {
            return await _context.Levels
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.IsActive && l.LevelId == id);
        }

        public async Task<Level?> GetForUpdateAsync(int id)
        {
            return await _context.Levels
                .FirstOrDefaultAsync(l => l.IsActive && l.LevelId == id);
        }

        public async Task AddAsync(Level level)
        {
            await _context.Levels.AddAsync(level);
        }

        public void Update(Level level)
        {
            _context.Levels.Update(level);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Levels
                .AsNoTracking()
                .AnyAsync(l => l.IsActive && l.Name == name);
        }

        // Legacy methods for backward compatibility
        public async Task UpdateAsync(Level level)
        {
            _context.Levels.Update(level);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Level level)
        {
            level.IsActive = false;
            _context.Levels.Update(level);
            await _context.SaveChangesAsync();
        }
    }
}
