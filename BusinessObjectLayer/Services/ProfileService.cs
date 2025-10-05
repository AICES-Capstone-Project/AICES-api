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
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;
       
        private readonly Cloudinary _cloudinary;

        public ProfileService(IProfileRepository profileRepository, Cloudinary cloudinary)
        {
            _profileRepository = profileRepository;
             _cloudinary = cloudinary;
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName)
        {
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName,
                IsActive = true,
            };
            return await _profileRepository.AddAsync(profile);
        }

        public async Task<Profile> CreateDefaultProfileAsync(int userId, string fullName, string avatarUrl)
        {
            var profile = new Profile
            {
                UserId = userId,
                FullName = fullName,
                AvatarUrl = avatarUrl,
                IsActive = true,
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
                Message = "Profile updated successfully.",
                Data = new ProfileResponse
                {
                    UserId = profile.UserId,
                    FullName = profile.FullName,
                    Address = profile.Address,
                    DateOfBirth = profile.DateOfBirth,
                    PhoneNumber = profile.PhoneNumber,
                    AvatarUrl = profile.AvatarUrl
                }
            };
        }

        private async Task<(bool Success, string? Url, string? ErrorMessage)> UploadAvatarAsync(int userId, Microsoft.AspNetCore.Http.IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                return (false, null, "Invalid file type. Only JPG, PNG, GIF, WEBP allowed.");
            }

            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
            {
                return (false, null, "File size exceeds 5MB limit.");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = $"avatars/{userId}_{DateTime.UtcNow.Ticks}",
                    Folder = "avatars",
                    Transformation = new Transformation().Width(500).Height(500).Crop("fill")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                
                if (uploadResult.Error != null)
                {
                    return (false, null, $"Upload failed: {uploadResult.Error.Message}");
                }

                return (true, uploadResult.SecureUrl.ToString(), null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Upload error: {ex.Message}");
            }
        }
    }
}
