using Data.Entities;
using Data.Enum;
using Data.Models.Response;
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

        public async Task<IEnumerable<Company>> GetAllAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync();
        }

        public async Task<List<Company>> GetCompaniesAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Companies.AsNoTracking().AsQueryable();

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

        public async Task<List<CompanyResponse>> GetCompaniesWithCreatorAsync(int page, int pageSize, string? search = null, CompanyStatusEnum? status = null)
        {
            var query = _context.Companies.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) ||
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Address != null && c.Address.Contains(search)));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.CompanyStatus == status.Value && c.IsActive);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CompanyResponse
                {
                    CompanyId = c.CompanyId,
                    Name = c.Name,
                    Address = c.Address,
                    LogoUrl = c.LogoUrl,
                    CompanyStatus = c.CompanyStatus.ToString(),
                    CreatedBy = _context.Users
                        .Where(u => u.UserId == c.CreatedBy)
                        .Select(u => u.Profile != null ? u.Profile.FullName : null)
                        .FirstOrDefault(),
                    ApprovalBy = c.ApprovedBy != null
                        ? _context.Users
                            .Where(u => u.UserId == c.ApprovedBy)
                            .Select(u => u.Profile != null ? u.Profile.FullName : null)
                            .FirstOrDefault()
                        : null,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> CountAsync(string? search = null, CompanyStatusEnum? status = null)
        {
            var query = _context.Companies.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) || 
                                       (c.Description != null && c.Description.Contains(search)) ||
                                       (c.Address != null && c.Address.Contains(search)));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.CompanyStatus == status.Value  && c.IsActive);
            }

            return await query.CountAsync();
        }

        public async Task<List<Company>> GetPublicCompaniesAsync()
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Company?> GetPublicByIdAsync(int id)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                .FirstOrDefaultAsync(c => c.CompanyId == id);
        }

        public async Task<Company?> GetByIdAsync(int id)
        {
            return await _context.Companies
                .AsNoTracking()
                .Include(c => c.CompanyUsers!)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Role)
                .Include(c => c.CompanyDocuments)
                .FirstOrDefaultAsync(c => c.CompanyId == id);
        }

        public async Task<CompanyDetailResponse?> GetByIdWithCreatorAsync(int id)
        {
            return await _context.Companies
                .AsNoTracking()
                .Where(c => c.CompanyId == id)
                .Select(c => new CompanyDetailResponse
                {
                    CompanyId = c.CompanyId,
                    Name = c.Name,
                    Description = c.Description,
                    Address = c.Address,
                    WebsiteUrl = c.Website,
                    TaxCode = c.TaxCode,
                    LogoUrl = c.LogoUrl,
                    CompanyStatus = c.CompanyStatus.ToString(),
                    CreatedBy = _context.Users
                        .Where(u => u.UserId == c.CreatedBy)
                        .Select(u => u.Profile != null ? u.Profile.FullName : null)
                        .FirstOrDefault(),
                    ApprovalBy = c.ApprovedBy != null
                        ? _context.Users
                            .Where(u => u.UserId == c.ApprovedBy)
                            .Select(u => u.Profile != null ? u.Profile.FullName : null)
                            .FirstOrDefault()
                        : null,
                    RejectionReason = c.RejectReason,
                    CreatedAt = c.CreatedAt,
                    Documents = c.CompanyDocuments != null 
                        ? c.CompanyDocuments.Select(d => new CompanyDocumentResponse
                        {
                            DocumentType = d.DocumentType ?? string.Empty,
                            FileUrl = d.FileUrl ?? string.Empty
                        }).ToList() 
                        : new List<CompanyDocumentResponse>()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Company?> GetByIdForUpdateAsync(int id)
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
            await _context.Companies.AddAsync(company);
            return company;
        }

        public async Task UpdateAsync(Company company)
        {
            _context.Companies.Update(company);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Name == name);
        }

        public async Task<bool> ExistsAsync(int companyId)
        {
            return await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.CompanyId == companyId);
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
            var newRole = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleName == newRoleName);
            if (newRole == null)
                return false;

            // Cập nhật role
            companyUser.User.RoleId = newRole.RoleId;

            // Update joinStatus to Approved
            companyUser.JoinStatus = JoinStatusEnum.Approved;

            _context.Users.Update(companyUser.User);
            _context.CompanyUsers.Update(companyUser);

            return true;
        }

    }
}
