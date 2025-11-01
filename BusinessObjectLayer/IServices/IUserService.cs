using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IUserService
    {
        Task<ServiceResponse> CreateUserAsync(UserRequest request);
        Task<ServiceResponse> GetUserByIdAsync(int id);
        Task<ServiceResponse> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> UpdateUserAsync(int id, UserRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
        Task<ServiceResponse> RestoreAsync(int id);
        
    }
}
