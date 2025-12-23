using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeService
    {
        Task<ServiceResponse> UploadResumeAsync(int campaignId, int jobId, IFormFile file);
        Task<ServiceResponse> UploadResumeBatchAsync(int campaignId, int jobId, IFormFileCollection files);
        Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request);
        // Task<ServiceResponse> RetryFailedResumeAsync(int resumeId);
        Task<ServiceResponse> SoftDeleteResumeAsync(int applicationId);
        // Task<ServiceResponse> ResendResumeAsync(int jobId, int resumeId);
    }
}

