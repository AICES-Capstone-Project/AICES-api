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
            return await _context.EmploymentTypes.ToListAsync();
        }

        public async Task<EmploymentType?> GetByIdAsync(int id)
        {
            return await _context.EmploymentTypes.FindAsync(id);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.EmploymentTypes.AnyAsync(e => e.Name == name);
        }

        public async Task<EmploymentType> AddAsync(EmploymentType employmentType)
        {
            _context.EmploymentTypes.Add(employmentType);
            await _context.SaveChangesAsync();
            return employmentType;
        }

        public async Task UpdateAsync(EmploymentType employmentType)
        {
            _context.EmploymentTypes.Update(employmentType);
            await _context.SaveChangesAsync();
        }
    }
}
