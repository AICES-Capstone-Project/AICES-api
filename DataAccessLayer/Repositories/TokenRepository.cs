using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class TokenRepository : ITokenRepository
    {
        private readonly AICESDbContext _context;

        public TokenRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .AsNoTracking()
                .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
                .Include(rt => rt.User)
                .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(rt => rt.IsActive && rt.Token == token);
        }

        public async Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
                .Include(rt => rt.User)
                .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(rt => rt.IsActive && rt.Token == token);
        }

        public async Task UpdateRefreshTokenAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Update(refreshToken);
        }

        public async Task RevokeAllRefreshTokensAsync(int userId)
        {
            var refreshTokens = await _context.RefreshTokens
                .Where(rt => rt.IsActive && rt.UserId == userId)
                .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsActive = false;
                _context.RefreshTokens.Update(token);
            }
        }
    }
}


