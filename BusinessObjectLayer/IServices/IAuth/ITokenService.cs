using Data.Entities;
using Data.Models.Response;
using System.Security.Claims;

namespace BusinessObjectLayer.Services.Auth
{
    public interface ITokenService
    {
        Task<AuthTokenResponse> GenerateTokensAsync(User user);
        Task<ServiceResponse> RefreshTokensAsync(string refreshToken);
        string GenerateVerificationToken(string email);
        string GenerateResetToken(string email);
        ClaimsPrincipal ValidateToken(string token);
    }
}

