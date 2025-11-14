using Data.Models.Request;
using Data.Models.Response;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICompanySubscriptionService
    {
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> CreateAsync(CreateCompanySubscriptionRequest request);
        Task<ServiceResponse> UpdateAsync(int id, CompanySubscriptionRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
    }
}

