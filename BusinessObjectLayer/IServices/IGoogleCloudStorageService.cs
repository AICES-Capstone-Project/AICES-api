using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.IServices
{
    public interface IGoogleCloudStorageService
    {
        Task<string> UploadFileAsync(IFormFile file);
    }
}


