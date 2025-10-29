using Data.Models.Request;
using Data.Models.Response;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ISpecializationService
    {
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> CreateAsync(SpecializationRequest request);
        Task<ServiceResponse> UpdateAsync(int id, SpecializationRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
    }
}

