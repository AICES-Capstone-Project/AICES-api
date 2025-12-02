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
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<EmploymentType?> GetByIdAsync(int id)
        {
            return await _context.EmploymentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(et => et.EmployTypeId == id);
        }

        public async Task<EmploymentType?> GetByIdForUpdateAsync(int id)
        {
            return await _context.EmploymentTypes
                .FirstOrDefaultAsync(et => et.EmployTypeId == id);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.EmploymentTypes
                .AsNoTracking()
                .AnyAsync(e => e.Name == name);
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

        public async Task UpdateAsync(EmploymentType employmentType)
        {
            _context.EmploymentTypes.Update(employmentType);
            await Task.CompletedTask;
        }
    }
}
