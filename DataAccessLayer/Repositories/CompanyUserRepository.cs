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
    }
} 



