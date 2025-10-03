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
    public class AuthRepository : IAuthRepository
    {
        private readonly AICESDbContext _context;

        public AuthRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> RoleExistsAsync(int roleId)
        {
            return await _context.Roles.AnyAsync(r => r.RoleId == roleId);
        }

        public async Task<User> GetByProviderAsync(string provider, string providerId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Profile)
                .Include(u => u.LoginProviders)
                .FirstOrDefaultAsync(u => u.LoginProviders.Any(lp => lp.AuthProvider == provider && lp.ProviderId == providerId));
        }

        public async Task<LoginProvider> AddLoginProviderAsync(LoginProvider loginProvider)
        {
            _context.LoginProviders.Add(loginProvider);
            await _context.SaveChangesAsync();
            return loginProvider;
        }

        public async Task<LoginProvider?> GetLoginProviderAsync(int userId, string provider)
        {
            return await _context.LoginProviders
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.AuthProvider == provider);
        }

        // Refresh Token methods
        public async Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
                .Include(rt => rt.User)
                .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task UpdateRefreshTokenAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task RevokeAllRefreshTokensAsync(int userId)
        {
            var refreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive)
                .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsActive = false;
            }

            await _context.SaveChangesAsync();
        }
    }
}
