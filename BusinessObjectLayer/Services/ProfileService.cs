using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
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
        private readonly IUnitOfWork _uow;
        private readonly Common.CloudinaryHelper _cloudinaryHelper;

        public ProfileService(IUnitOfWork uow, Common.CloudinaryHelper cloudinaryHelper)
        {
            _uow = uow;
            _cloudinaryHelper = cloudinaryHelper;
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName)
        {
            var profileRepo = _uow.GetRepository<IProfileRepository>();
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName
            };
            var result = await profileRepo.AddAsync(profile);
            await _uow.SaveChangesAsync();
            return result;
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName, string avatarUrl)
        {
            var profileRepo = _uow.GetRepository<IProfileRepository>();
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName,
                AvatarUrl = avatarUrl
            };
            var result = await profileRepo.AddAsync(profile);
            await _uow.SaveChangesAsync();
            return result;
        }

        public async Task<Profile> GetByUserIdAsync(int userId)
        {
            var profileRepo = _uow.GetRepository<IProfileRepository>();
            return await profileRepo.GetByUserIdAsync(userId);
        }

        public async Task UpdateAsync(Profile profile)
        {
            var profileRepo = _uow.GetRepository<IProfileRepository>();
            await profileRepo.UpdateAsync(profile);
            await _uow.SaveChangesAsync();
        }

        public async Task<ServiceResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var profileRepo = _uow.GetRepository<IProfileRepository>();
            var profile = await profileRepo.GetByUserIdAsync(userId);
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
                profile.DateOfBirth = DateTime.SpecifyKind(
                request.DateOfBirth.Value.Date,
                DateTimeKind.Utc
 );


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

            await profileRepo.UpdateAsync(profile);
            await _uow.SaveChangesAsync();

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
