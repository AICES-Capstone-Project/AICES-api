using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace BusinessObjectLayer.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;
        private readonly IWebHostEnvironment _environment;

        public ProfileService(IProfileRepository profileRepository, IWebHostEnvironment environment)
        {
            _profileRepository = profileRepository;
            _environment = environment;
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
            if (request.DateOfBirth.HasValue) profile.DateOfBirth = request.DateOfBirth;
            if (!string.IsNullOrEmpty(request.PhoneNumber)) profile.PhoneNumber = request.PhoneNumber;

            // Xử lý upload ảnh nếu có
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

                // Lưu file vào wwwroot/avatars
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.AvatarFile.CopyToAsync(stream);
                }

                // Cập nhật AvatarUrl
                profile.AvatarUrl = $"/avatars/{fileName}";
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
                    AvatarUrl = profile.AvatarUrl,
                    PhoneNumber = profile.PhoneNumber
                }
            };
        }
    }
}
