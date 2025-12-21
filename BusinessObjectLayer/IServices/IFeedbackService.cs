using Data.Models.Request;
using Data.Models.Response;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IFeedbackService
    {
        Task<ServiceResponse> CreateAsync(FeedbackRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10);
        Task<ServiceResponse> GetByIdAsync(int feedbackId);
        Task<ServiceResponse> GetMyFeedbacksAsync(ClaimsPrincipal userClaims, int page = 1, int pageSize = 10);
        Task<ServiceResponse> DeleteMyFeedbackAsync(int feedbackId, ClaimsPrincipal userClaims);
        Task<ServiceResponse> DeleteFeedbackByAdminAsync(int feedbackId);
    }
}
