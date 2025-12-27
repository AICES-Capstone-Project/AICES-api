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
            await _context.CompanyUsers.AddAsync(companyUser);
            return companyUser;
        }

        public async Task<CompanyUser?> GetByUserIdAsync(int userId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(cu => cu.IsActive && cu.UserId == userId);
        }

        public async Task<CompanyUser?> GetCompanyUserByUserIdAsync(int userId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)  // Include User to get email
                .Include(cu => cu.Company)
                .FirstOrDefaultAsync(cu => cu.IsActive && cu.UserId == userId);
        }

        public async Task<CompanyUser?> GetByComUserIdAsync(int comUserId)
        {
            return await _context.CompanyUsers
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Include(cu => cu.Company)
                .FirstOrDefaultAsync(cu => cu.IsActive && cu.ComUserId == comUserId);
        }

        public async Task<bool> ExistsAsync(int comUserId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .AnyAsync(cu => cu.IsActive && cu.ComUserId == comUserId);
        }

        public async Task UpdateAsync(CompanyUser companyUser)
        {
            _context.CompanyUsers.Update(companyUser);
        }

        public async Task<List<CompanyUser>> GetMembersByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.IsActive && cu.CompanyId == companyId && cu.User != null)
                .ToListAsync();
        }

        public async Task<List<CompanyUser>> GetPendingByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.IsActive && cu.CompanyId == companyId && cu.JoinStatus == JoinStatusEnum.Pending)
                .ToListAsync();
        }

        public async Task<List<CompanyUser>> GetApprovedAndInvitedMembersByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.IsActive && cu.CompanyId == companyId && cu.User != null && 
                    (cu.JoinStatus == JoinStatusEnum.Approved))
                .ToListAsync();
        }

        public async Task<List<CompanyUser>> GetHrManagersByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.IsActive 
                             && cu.CompanyId == companyId 
                             && cu.User != null 
                             && cu.JoinStatus == JoinStatusEnum.Approved
                             && cu.User.RoleId == 4) // HR_Manager
                .ToListAsync();
        }

        public async Task<List<User>> GetHrManagerUsersByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyUsers
                .AsNoTracking()
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Profile)
                .Include(cu => cu.User)
                    .ThenInclude(u => u.Role)
                .Where(cu => cu.IsActive 
                             && cu.CompanyId == companyId 
                             && cu.User != null 
                             && cu.JoinStatus == JoinStatusEnum.Approved
                             && cu.User.Role!.RoleId == 4) // HR_Manager
                .Select(cu => cu.User!)
                .Distinct()
                .ToListAsync();
        }
    }
} 



