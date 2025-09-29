using BusinessObjectLayer.IServices;
using Data.Entities;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;

        public ProfileService(IProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
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
    }
}
