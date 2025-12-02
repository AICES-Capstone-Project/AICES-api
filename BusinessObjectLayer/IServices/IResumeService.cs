using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeService
    {
        Task<ServiceResponse> UploadResumeAsync(int jobId, IFormFile file);
        Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request);
        Task<ServiceResponse> GetJobResumesAsync(int jobId);
        Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int resumeId);
        Task<ServiceResponse> RetryFailedResumeAsync(int resumeId);
        Task<ServiceResponse> SoftDeleteResumeAsync(int resumeId);
        Task<ServiceResponse> ResendResumeAsync(int jobId, int resumeId);
    }
}

