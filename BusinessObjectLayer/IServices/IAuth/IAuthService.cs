using Data.Entities;

using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices.Auth
{
    public interface IAuthService
    {
        Task<ServiceResponse> RegisterAsync(string email, string password, string fullName); 
        Task<ServiceResponse> LoginAsync(string email, string password);
        Task<ServiceResponse> VerifyEmailAsync(string token);
        Task<ServiceResponse> GoogleLoginAsync(string accessToken);
        Task<ServiceResponse> GitHubLoginAsync(string code);
        Task<ServiceResponse> RequestPasswordResetAsync(string email);
        Task<ServiceResponse> ResetPasswordAsync(string token, string newPassword);
        Task<ServiceResponse> GetCurrentUserInfoAsync(ClaimsPrincipal userClaims);
        Task<ServiceResponse> RefreshTokenAsync(string refreshToken);
        Task<ServiceResponse> LogoutAsync(string refreshToken);
        Task<ServiceResponse> ChangePasswordAsync(ClaimsPrincipal userClaims, string oldPassword, string newPassword);
    }
}
