using BusinessObjectLayer.IServices;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BusinessObjectLayer.Services
{
    public class GoogleCloudStorageService : IGoogleCloudStorageService
    {
        private readonly string _bucketName;
        private readonly StorageClient _storageClient;

        public GoogleCloudStorageService(IConfiguration configuration)
        {
            _bucketName = configuration["GCP:BUCKET_NAME"] ?? 
                         Environment.GetEnvironmentVariable("GCP__BUCKET_NAME") ?? 
                         throw new ArgumentNullException(nameof(_bucketName), "GCP Bucket Name is not configured");

            string? credentialPath = configuration["GCP:CREDENTIAL_PATH"] ?? 
                                    Environment.GetEnvironmentVariable("GCP__CREDENTIAL_PATH");

            if (string.IsNullOrEmpty(credentialPath))
            {
                throw new ArgumentNullException(nameof(credentialPath), "GCP Credential Path is not configured");
            }

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
            _storageClient = StorageClient.Create();
        }

        public async Task<string> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty or null", nameof(file));
            }

            using var stream = file.OpenReadStream();
            var objectName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            
            await _storageClient.UploadObjectAsync(_bucketName, objectName, null, stream);
            
            return $"https://storage.googleapis.com/{_bucketName}/{objectName}";
        }
    }
}


