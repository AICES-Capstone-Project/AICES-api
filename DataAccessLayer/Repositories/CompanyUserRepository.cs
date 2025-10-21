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

        public async Task UpdateAsync(CompanyUser companyUser)
        {
            _context.CompanyUsers.Update(companyUser);
            await _context.SaveChangesAsync();
        }
    }
} 



