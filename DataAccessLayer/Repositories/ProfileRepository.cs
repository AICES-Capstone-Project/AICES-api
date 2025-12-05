using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly AICESDbContext _context;

        public ProfileRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Profile> AddAsync(Profile profile)
        {
            await _context.Profiles.AddAsync(profile);
            return profile;
        }

        public async Task<Profile?> GetByUserIdAsync(int userId)
        {
            return await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsActive && p.UserId == userId);
        }

        public async Task<Profile?> GetForUpdateByUserIdAsync(int userId)
        {
            return await _context.Profiles
                .FirstOrDefaultAsync(p => p.IsActive && p.UserId == userId);
        }

        public async Task UpdateAsync(Profile profile)
        {
            _context.Profiles.Update(profile);
        }
    }
}
