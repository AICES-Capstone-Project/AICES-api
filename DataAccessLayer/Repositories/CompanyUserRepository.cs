using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class CompanyUserRepository : ICompanyUserRepository
    {
        private readonly AICESDbContext _context;

        public CompanyUserRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<CompanyUser> AddCompanyUserAsync(CompanyUser companyUser)
        {
            _context.CompanyUsers.Add(companyUser);
            await _context.SaveChangesAsync();
            return companyUser;
        }

        public async Task<CompanyUser?> GetByUserIdAsync(int userId)
        {
            return await _context.CompanyUsers
                .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.IsActive);
        }

        public async Task<CompanyUser?> GetCompanyUserByUserIdAsync(int userId)
        {
            return await _context.CompanyUsers
                .Include(cu => cu.Company)
                .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.IsActive);
        }

        public async Task<CompanyUser?> GetByComUserIdAsync(int comUserId)
        {
            return await _context.CompanyUsers
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Include(cu => cu.Company)
                .FirstOrDefaultAsync(cu => cu.ComUserId == comUserId && cu.IsActive);
        }

        public async Task<bool> ExistsAsync(int comUserId)
        {
            return await _context.CompanyUsers.AnyAsync(cu => cu.ComUserId == comUserId);
        }

        public async Task UpdateAsync(CompanyUser companyUser)
        {
            _context.CompanyUsers.Update(companyUser);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CompanyUser>> GetMembersByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.CompanyId == companyId && cu.IsActive && cu.User != null)
                .ToListAsync();
        }

        public async Task<List<CompanyUser>> GetPendingByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.CompanyId == companyId && cu.IsActive && cu.JoinStatus == JoinStatusEnum.Pending)
                .ToListAsync();
        }
    }
} 



