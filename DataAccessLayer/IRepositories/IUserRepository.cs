using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetForUpdateAsync(int id);
        Task<List<User>> GetUsersAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalUsersAsync(string? search = null);
        Task<User> AddAsync(User user);
        Task UpdateAsync(User user);
        Task<bool> EmailExistsAsync(string email);
        Task<LoginProvider> AddLoginProviderAsync(LoginProvider loginProvider);
    }
}
