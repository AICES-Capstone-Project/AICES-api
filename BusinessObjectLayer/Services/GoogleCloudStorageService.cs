using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Response;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace BusinessObjectLayer.Services
{
    public class GoogleCloudStorageService : IGoogleCloudStorageService
    {
        private readonly string _bucketName;
        private readonly StorageClient _storageClient;
        private readonly GoogleCloudStorageHelper _helper;

        public GoogleCloudStorageService(IConfiguration configuration)
        {
            _bucketName = configuration["GCP:BUCKET_NAME"] ?? 
                         Environment.GetEnvironmentVariable("GCP__BUCKET_NAME") ?? 
                         throw new ArgumentNullException(nameof(_bucketName), "GCP Bucket Name is not configured");

            // Check if service-account.json exists in the current directory first
            var serviceAccountPath = Path.Combine(Directory.GetCurrentDirectory(), "service-account.json");
            string? credentialPath = null;
            
            if (File.Exists(serviceAccountPath))
            {
                // Use service-account.json if it exists in current directory
                credentialPath = serviceAccountPath;
                Console.WriteLine($"✅ Using service account file: {credentialPath}");
            }
            else
            {
                // Try to get configured credential path
                credentialPath = configuration["GCP:CREDENTIAL_PATH"] ?? 
                                Environment.GetEnvironmentVariable("GCP__CREDENTIAL_PATH");
                
                if (!string.IsNullOrEmpty(credentialPath))
                {
                    if (File.Exists(credentialPath))
                    {
                        Console.WriteLine($"✅ Using configured credential path: {credentialPath}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Configured credential path not found: {credentialPath}");
                        credentialPath = null;
                    }
                }
            }

            // Try to create StorageClient with credentials or use Application Default Credentials
            try
            {
                if (!string.IsNullOrEmpty(credentialPath) && File.Exists(credentialPath))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
                    _storageClient = StorageClient.Create();
                    _helper = new GoogleCloudStorageHelper(_bucketName, credentialPath);
                    Console.WriteLine("✅ Google Cloud Storage initialized with service account file");
                }
                else
                {
                    // Use Application Default Credentials (works with Workload Identity on GCP)
                    _storageClient = StorageClient.Create();
                    _helper = new GoogleCloudStorageHelper(_bucketName, null); // null means use ADC
                    Console.WriteLine("✅ Google Cloud Storage initialized with Application Default Credentials");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to initialize Google Cloud Storage: {ex.Message}");
                throw new InvalidOperationException(
                    "Failed to initialize Google Cloud Storage. Make sure credentials are configured correctly.", ex);
            }
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

        /// <summary>
        /// Upload a resume file (PDF or DOCX) using the helper
        /// </summary>
        public async Task<ServiceResponse> UploadResumeAsync(IFormFile file, string? folder = "resumes")
        {
            return await _helper.UploadResumeAsync(file, folder);
        }

        /// <summary>
        /// Upload a user's resume with user ID prefix
        /// </summary>
        public async Task<ServiceResponse> UploadUserResumeAsync(int userId, IFormFile file)
        {
            return await _helper.UploadUserResumeAsync(userId, file);
        }
    }
}


