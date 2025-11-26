using Data.Entities;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ITokenRepository
    {
        Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string token);
        Task UpdateRefreshTokenAsync(RefreshToken refreshToken);
        Task RevokeAllRefreshTokensAsync(int userId);
    }
}


