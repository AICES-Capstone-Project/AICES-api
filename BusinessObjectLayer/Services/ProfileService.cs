using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;
        private readonly Common.CloudinaryHelper _cloudinaryHelper;

        public ProfileService(IProfileRepository profileRepository, Common.CloudinaryHelper cloudinaryHelper)
        {
            _profileRepository = profileRepository;
            _cloudinaryHelper = cloudinaryHelper;
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName)
        {
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName
            };
            return await _profileRepository.AddAsync(profile);
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName, string avatarUrl)
        {
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName,
                AvatarUrl = avatarUrl
            };
            return await _profileRepository.AddAsync(profile);
        }

        public async Task<Profile> GetByUserIdAsync(int userId)
        {
            return await _profileRepository.GetByUserIdAsync(userId);
        }

        public async Task UpdateAsync(Profile profile)
        {
            await _profileRepository.UpdateAsync(profile);
        }

        public async Task<ServiceResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var profile = await _profileRepository.GetByUserIdAsync(userId);
            if (profile == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Profile not found."
                };
            }

            if (!string.IsNullOrEmpty(request.FullName)) 
                profile.FullName = request.FullName;
            
            if (!string.IsNullOrEmpty(request.Address)) 
                profile.Address = request.Address;
            
            if (request.DateOfBirth.HasValue) 
                profile.DateOfBirth = request.DateOfBirth.Value.Date;
            
            if (!string.IsNullOrEmpty(request.PhoneNumber)) 
                profile.PhoneNumber = request.PhoneNumber;

            if (request.AvatarFile != null && request.AvatarFile.Length > 0)
            {
                var avatarUploadResult = await UploadAvatarAsync(userId, request.AvatarFile);
                if (!avatarUploadResult.Success)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = avatarUploadResult.ErrorMessage
                    };
                }
                profile.AvatarUrl = avatarUploadResult.Url;
            }

            await _profileRepository.UpdateAsync(profile);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Profile updated successfully."
            };
        }

        public async Task<ServiceResponse> UpdateProfileFromClaimsAsync(ClaimsPrincipal user, UpdateProfileRequest request)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(user);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid user ID format."
                };
            }

            return await UpdateProfileAsync(userId, request);
        }

        private async Task<(bool Success, string? Url, string? ErrorMessage)> UploadAvatarAsync(int userId, Microsoft.AspNetCore.Http.IFormFile file)
        {
            return await _cloudinaryHelper.UploadAvatarAsync(userId, file);
        }
    }
}
