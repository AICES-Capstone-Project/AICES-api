using Data.Models.Request;
using Data.Models.Response;
using System.Security.Claims;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeApplicationService
    {
        Task<ServiceResponse> UpdateAdjustedScoreAsync(int applicationId, UpdateAdjustedScoreRequest request, ClaimsPrincipal user);
        Task<ServiceResponse> UpdateStatusAsync(int applicationId, UpdateApplicationStatusRequest request, ClaimsPrincipal user);
        Task<ServiceResponse> GetJobResumesAsync(int jobId, int campaignId, GetJobResumesRequest request);
        Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int applicationId, int campaignId);
    }
}
