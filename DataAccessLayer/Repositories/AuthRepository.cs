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
    public class AuthRepository : IAuthRepository
    {
        private readonly AICESDbContext _context;

        public AuthRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .FirstOrDefaultAsync(u => u.IsActive && u.Email == email);
        }

        public async Task<User?> GetForUpdateByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .FirstOrDefaultAsync(u => u.IsActive && u.Email == email);
        }

        public async Task<User> AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            return user;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.IsActive && u.Email == email);
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
        }

        public async Task<bool> RoleExistsAsync(int roleId)
        {
            return await _context.Roles
                .AsNoTracking()
                .AnyAsync(r => r.RoleId == roleId);
        }

        public async Task<User> GetByProviderAsync(AuthProviderEnum provider, string providerId)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.LoginProviders)
                .FirstOrDefaultAsync(u => u.IsActive && u.LoginProviders.Any(lp => lp.AuthProvider == provider && lp.ProviderId == providerId));
        }

        public async Task<LoginProvider> AddLoginProviderAsync(LoginProvider loginProvider)
        {
            await _context.LoginProviders.AddAsync(loginProvider);
            return loginProvider;
        }

        public async Task<LoginProvider?> GetLoginProviderAsync(int userId, AuthProviderEnum provider)
        {
            return await _context.LoginProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(lp => lp.IsActive && lp.UserId == userId && lp.AuthProvider == provider);
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(string roleName)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Where(u => u.IsActive && u.Role.RoleName == roleName)
                .ToListAsync();
        }
    }
}
