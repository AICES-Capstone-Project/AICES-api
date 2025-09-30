using Data.Entities;

using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IAuthService
    {
        Task<ServiceResponse> RegisterAsync(string email, string password, string fullName); 
        Task<ServiceResponse> LoginAsync(string email, string password);
        Task<ServiceResponse> VerifyEmailAsync(string token);
        Task<ServiceResponse> GoogleLoginAsync(string googleToken);
        Task<ServiceResponse> GetCurrentUserInfoAsync(ClaimsPrincipal userClaims);
    }
}
