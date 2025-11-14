using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ISubscriptionService
    {
        Task<ServiceResponse> GetAllByAdminAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetByIdForAdminAsync(int id);
        Task<ServiceResponse> CreateAsync(SubscriptionRequest request);
        Task<ServiceResponse> UpdateAsync(int id, SubscriptionRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
    }
}
