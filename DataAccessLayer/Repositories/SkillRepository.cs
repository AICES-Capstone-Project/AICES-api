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
    public class SkillRepository : ISkillRepository
    {
        private readonly AICESDbContext _context;

        public SkillRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Skill>> GetAllAsync()
        {
            return await _context.Skills
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Skill> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Skills
                .AsNoTracking()
                .Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.Name.Contains(search));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<Skill?> GetByIdAsync(int id)
        {
            return await _context.Skills
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive && s.SkillId == id);
        }

        public async Task<Skill?> GetForUpdateAsync(int id)
        {
            return await _context.Skills
                .FirstOrDefaultAsync(s => s.IsActive && s.SkillId == id);
        }

        public async Task AddAsync(Skill skill)
        {
            await _context.Skills.AddAsync(skill);
        }

        public void Update(Skill skill)
        {
            _context.Skills.Update(skill);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Skills
                .AsNoTracking()
                .AnyAsync(s => s.IsActive && s.Name == name);
        }

        // Legacy methods for backward compatibility
        public async Task UpdateAsync(Skill skill)
        {
            _context.Skills.Update(skill);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Skill skill)
        {
            skill.IsActive = false;
            _context.Skills.Update(skill);
            await _context.SaveChangesAsync();
        }
    }
}
