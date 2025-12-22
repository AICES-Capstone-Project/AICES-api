using Data.Models.Response;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public interface IStorageHelper
    {
        Task<ServiceResponse> UploadFileAsync(IFormFile file, string folder = null, string publicId = null);
        Task<ServiceResponse> DeleteFileAsync(string publicId);
        Task<ServiceResponse> GetSignedUrlAsync(string publicId, int expirationMinutes = 60);
    }
}

