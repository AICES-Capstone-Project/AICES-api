using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeService
    {
        Task<ServiceResponse> UploadResumeAsync(int jobId, IFormFile file);
        Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request);
        Task<ServiceResponse> GetResumeResultAsync(int resumeId);
    }
}

