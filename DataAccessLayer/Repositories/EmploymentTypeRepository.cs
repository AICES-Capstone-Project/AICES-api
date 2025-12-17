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
    public class EmploymentTypeRepository : IEmploymentTypeRepository
    {
        private readonly AICESDbContext _context;

        public EmploymentTypeRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<EmploymentType>> GetAllAsync()
        {
            return await _context.EmploymentTypes
                .Where(et => et.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<(IEnumerable<EmploymentType> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.EmploymentTypes
                .AsNoTracking()
                .Where(et => et.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(et => et.Name.Contains(search));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(et => et.EmployTypeId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<EmploymentType?> GetByIdAsync(int id)
        {
            return await _context.EmploymentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(et => et.IsActive && et.EmployTypeId == id);
        }

        public async Task<EmploymentType?> GetForUpdateAsync(int id)
        {
            return await _context.EmploymentTypes
                .FirstOrDefaultAsync(et => et.IsActive && et.EmployTypeId == id);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.EmploymentTypes
                .AsNoTracking()
                .AnyAsync(e => e.IsActive && e.Name == name);
        }

        public async Task<bool> ExistsAsync(int employmentTypeId)
        {
            return await _context.EmploymentTypes
                .AsNoTracking()
                .AnyAsync(et => et.EmployTypeId == employmentTypeId && et.IsActive);
        }

        public async Task AddAsync(EmploymentType employmentType)
        {
            await _context.EmploymentTypes.AddAsync(employmentType);
        }

        public void Update(EmploymentType employmentType)
        {
            _context.EmploymentTypes.Update(employmentType);
        }

        // Legacy method for backward compatibility
        public async Task UpdateAsync(EmploymentType employmentType)
        {
            _context.EmploymentTypes.Update(employmentType);
            await _context.SaveChangesAsync();
        }
    }
}
