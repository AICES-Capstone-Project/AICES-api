using System.Threading.Tasks;
using Data.Models.Request;
using ServiceResponse = Data.Models.Response.ServiceResponse;

namespace BusinessObjectLayer.IServices
{
    public interface ICandidateService
    {
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> GetByIdWithResumesAsync(int id);
        Task<ServiceResponse> CreateAsync(CandidateCreateRequest request);
        Task<ServiceResponse> UpdateAsync(int id, CandidateUpdateRequest request);
        Task<ServiceResponse> DeleteAsync(int id);
        Task<ServiceResponse> GetResumeApplicationsAsync(int resumeId);
        Task<ServiceResponse> GetResumeApplicationDetailAsync(int resumeId, int applicationId);
        Task<ServiceResponse> GetResumesByCandidateAsync(int candidateId);
    }
}


