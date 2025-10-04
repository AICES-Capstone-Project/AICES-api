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
                    Status = SRStatus.Error,
                    Message = "Profile not found. Please create a profile first."
                };
            }

            // Cập nhật thông tin text
            if (!string.IsNullOrEmpty(request.FullName)) profile.FullName = request.FullName;
            if (!string.IsNullOrEmpty(request.Address)) profile.Address = request.Address;
            if (request.DateOfBirth.HasValue) profile.DateOfBirth = request.DateOfBirth?.Date;
            if (!string.IsNullOrEmpty(request.PhoneNumber)) profile.PhoneNumber = request.PhoneNumber;

            // Xử lý upload avatar nếu có
            if (request.AvatarFile != null && request.AvatarFile.Length > 0)
            {
                // Kiểm tra loại file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(request.AvatarFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Invalid file type. Only JPG, PNG, GIF allowed."
                    };
                }

                // Đọc stream thành byte[]
                using var stream = request.AvatarFile.OpenReadStream();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Upload lên Cloudinary với byte[]
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(request.AvatarFile.FileName, memoryStream), 
                    PublicId = $"avatars/{userId}_{DateTime.UtcNow.Ticks}",
                    Folder = "avatars"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                var avatarUrl = uploadResult.SecureUrl.ToString();
                profile.AvatarUrl = avatarUrl;
            }

            await _profileRepository.UpdateAsync(profile);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Profile updated successfully.",
                Data = new
                {
                    FullName = profile.FullName,
                    Address = profile.Address,
                    DateOfBirth = profile.DateOfBirth?.ToString("yyyy-MM-dd"),
                    PhoneNumber = profile.PhoneNumber,
                    AvatarUrl = profile.AvatarUrl
                }
            };
        }
    }
}
