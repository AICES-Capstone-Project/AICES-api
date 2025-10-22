using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly AICESDbContext _context;

        public CompanyRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.Companies.AsQueryable();

            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            return await query.ToListAsync();
        }

        public async Task<List<Company>> GetCompaniesAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Companies.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Address != null && c.Address.Contains(search)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCompaniesAsync(string? search = null)
        {
            var query = _context.Companies.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Address != null && c.Address.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Company>> GetPublicCompaniesAsync()
        {
            return await _context.Companies
                .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Company?> GetPublicByIdAsync(int id)
        {
            return await _context.Companies
                .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                .FirstOrDefaultAsync(c => c.CompanyId == id);
        }

        public async Task<Company?> GetByIdAsync(int id)
        {
            return await _context.Companies
                .Include(c => c.CompanyUsers!)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Role)
                .Include(c => c.CompanyDocuments)
                .FirstOrDefaultAsync(c => c.CompanyId == id);
        }


        public async Task<Company> AddAsync(Company company)
        {
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task UpdateAsync(Company company)
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Companies.AnyAsync(c => c.Name == name);
        }

        public async Task<bool> ExistsAsync(int companyId)
        {
            return await _context.Companies.AnyAsync(c => c.CompanyId == companyId);
        }

        public async Task<bool> UpdateUserRoleByCompanyAsync(int companyId, string newRoleName)
        {
            // Lấy user đầu tiên thuộc công ty
            var companyUser = await _context.CompanyUsers
                .Include(cu => cu.User)
                .FirstOrDefaultAsync(cu => cu.CompanyId == companyId);

            if (companyUser == null || companyUser.User == null)
                return false;

            // Lấy role HR_Manager
            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == newRoleName);
            if (newRole == null)
                return false;

            // Cập nhật role
            companyUser.User.RoleId = newRole.RoleId;

            _context.Users.Update(companyUser.User);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task AddCompanyUserAsync(CompanyUser companyUser)
        {
            _context.CompanyUsers.Add(companyUser);
            await _context.SaveChangesAsync();
        }



    }
}
