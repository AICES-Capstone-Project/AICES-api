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
    public class LanguageRepository : ILanguageRepository
    {
        private readonly AICESDbContext _context;

        public LanguageRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<Language>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var query = _context.Languages
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
            var query = _context.Languages
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

        public async Task<Language?> GetByIdAsync(int id)
        {
            return await _context.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.IsActive && l.LanguageId == id);
        }

        public async Task<Language?> GetForUpdateAsync(int id)
        {
            return await _context.Languages
                .FirstOrDefaultAsync(l => l.IsActive && l.LanguageId == id);
        }

        public async Task AddAsync(Language language)
        {
            await _context.Languages.AddAsync(language);
        }

        public void Update(Language language)
        {
            _context.Languages.Update(language);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Languages
                .AsNoTracking()
                .AnyAsync(l => l.IsActive && l.Name == name);
        }

        // Legacy methods for backward compatibility
        public async Task UpdateAsync(Language language)
        {
            _context.Languages.Update(language);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Language language)
        {
            language.IsActive = false;
            _context.Languages.Update(language);
            await _context.SaveChangesAsync();
        }
    }
}

