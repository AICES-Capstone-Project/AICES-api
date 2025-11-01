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
    public class UserRepository : IUserRepository
    {
        private readonly AICESDbContext _context;

        public UserRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<User> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.LoginProviders)
                .Include(u => u.CompanyUser)
                .Include(u => u.CompanyUser.Company)
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        public async Task<List<User>> GetUsersAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.LoginProviders)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Email.Contains(search) || (u.Profile != null && u.Profile.FullName != null && u.Profile.FullName.Contains(search)));
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalUsersAsync(string? search = null)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Email.Contains(search) || (u.Profile != null && u.Profile.FullName != null && u.Profile.FullName.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<User> AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<LoginProvider> AddLoginProviderAsync(LoginProvider loginProvider)
        {
            _context.LoginProviders.Add(loginProvider);
            await _context.SaveChangesAsync();
            return loginProvider;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }
    }
}
