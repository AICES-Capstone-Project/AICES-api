using Data.Entities;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ITokenRepository
    {
        Task<RefreshToken> AddAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetByTokenForUpdateAsync(string token);
        Task UpdateAsync(RefreshToken refreshToken);
        Task RevokeAllByUserIdAsync(int userId);
    }
}


