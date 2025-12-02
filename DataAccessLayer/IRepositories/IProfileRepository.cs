using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IProfileRepository
    {
        Task<Profile> AddAsync(Profile profile);
        Task<Profile?> GetByUserIdAsync(int userId);
        Task<Profile?> GetByUserIdForUpdateAsync(int userId);
        Task UpdateAsync(Profile profile);
    }
}
