using Data.Models.Request;
using Data.Models.Response;
using System.Security.Claims;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeApplicationService
    {
        Task<ServiceResponse> UpdateAdjustedScoreAsync(int applicationId, UpdateAdjustedScoreRequest request, ClaimsPrincipal user);
        Task<ServiceResponse> UpdateStatusAsync(int applicationId, UpdateApplicationStatusRequest request, ClaimsPrincipal user);
    }
}
