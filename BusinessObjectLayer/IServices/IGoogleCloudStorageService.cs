using Microsoft.AspNetCore.Http;
using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IGoogleCloudStorageService
    {
        Task<string> UploadFileAsync(IFormFile file);
        Task<ServiceResponse> UploadResumeAsync(IFormFile file, string? folder = "resumes");
    }
}


