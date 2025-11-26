using Data.Entities;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IAuthRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetForUpdateByEmailAsync(string email);
        Task<User> AddAsync(User user);
        Task<bool> EmailExistsAsync(string email);
        Task UpdateAsync(User user);
        Task<bool> RoleExistsAsync(int roleId);
        Task<User> GetByProviderAsync(AuthProviderEnum provider, string providerId);
        Task<LoginProvider> AddLoginProviderAsync(LoginProvider loginProvider);
        Task<LoginProvider?> GetLoginProviderAsync(int userId, AuthProviderEnum provider);
        Task<IEnumerable<User>> GetUsersByRoleAsync(string roleName);
    }
}
