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

        public async Task<Profile> CreateDefaultProfileAsync(int userId)
        {
            var profile = new Profile { UserId = userId };
            return await _profileRepository.AddAsync(profile);
        }
    }
}
