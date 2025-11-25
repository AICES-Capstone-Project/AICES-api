using Data.Entities;
using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IProfileService
    {
        Task<Profile> CreateDefaultProfileAsync(int userId, string fullName);
        Task<Profile> CreateDefaultProfileAsync(int userId, string fullName, string avatarUrl);
        Task<Profile> GetByUserIdAsync(int userId);
        Task UpdateAsync(Profile profile);
        Task<ServiceResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<ServiceResponse> UpdateProfileFromClaimsAsync(ClaimsPrincipal user, UpdateProfileRequest request);
    }
}

