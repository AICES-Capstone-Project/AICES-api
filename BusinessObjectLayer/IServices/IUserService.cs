using Data.Models.Request;
using Data.Models.Response;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IUserService
    {
        Task<ServiceResponse> CreateUserAsync(CreateUserRequest request);
        Task<ServiceResponse> GetUserByIdAsync(int id);
        Task<ServiceResponse> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> UpdateUserAsync(int id, UpdateUserRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
        Task<ServiceResponse> UpdateUserStatusAsync(int id, UserStatusEnum status);
        
    }
}
