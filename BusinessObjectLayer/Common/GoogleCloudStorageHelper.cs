using Data.Enum;
using Data.Models.Response;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public class GoogleCloudStorageHelper
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly UrlSigner _urlSigner;

        public GoogleCloudStorageHelper(string bucketName, string? credentialPath)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));

            GoogleCredential credential;
            
            if (!string.IsNullOrEmpty(credentialPath) && File.Exists(credentialPath))
            {
                // Use explicit credential file
                credential = GoogleCredential.FromFile(credentialPath);
                Console.WriteLine($"📁 GoogleCloudStorageHelper using credential file: {credentialPath}");
            }
            else
            {
                // Use Application Default Credentials (Workload Identity, metadata server, etc.)
                credential = GoogleCredential.GetApplicationDefault();
                Console.WriteLine("🔐 GoogleCloudStorageHelper using Application Default Credentials");
            }

            _storageClient = StorageClient.Create(credential);
            
            // UrlSigner requires service account credentials
            // For ADC in production, you might need to handle this differently
            try
            {
                _urlSigner = UrlSigner.FromCredential(credential);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not create UrlSigner: {ex.Message}");
                // UrlSigner might not work with ADC, but basic operations will still work
                _urlSigner = null!;
            }
        }

        /// <summary>
        /// Upload a resume file (PDF or DOCX) to Google Cloud Storage
        /// </summary>
        public async Task<ServiceResponse> UploadResumeAsync(
            IFormFile file,
            string? folder = "resumes",
            string? publicId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "File is empty or null."
                    };
                }

                // Validate file extension - only PDF and DOCX allowed for resumes
                var allowedExtensions = new[] { ".pdf", ".docx" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Invalid file type. Only PDF and DOCX files are allowed for resumes."
                    };
                }

                // Validate file size (10MB limit for resumes)
                const int maxFileSize = 10 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "File size exceeds 10MB limit."
                    };
                }

                using var stream = file.OpenReadStream();
                
                // Generate object name
                var objectName = publicId ?? $"{Guid.NewGuid()}{extension}";
                
                // Add folder prefix if specified
                if (!string.IsNullOrEmpty(folder))
                {
                    objectName = $"{folder.TrimEnd('/')}/{objectName}";
                }

                await _storageClient.UploadObjectAsync(_bucketName, objectName, null, stream);
                
                var signedUrl = _urlSigner.Sign(
                    _bucketName,
                    objectName,
                    TimeSpan.FromHours(1) // valid 1 hour
                );
                
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume uploaded successfully.",
                    Data = new { Url = signedUrl, ObjectName = objectName }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to upload resume: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Upload any file to Google Cloud Storage with optional folder
        /// </summary>
        public async Task<ServiceResponse> UploadFileAsync(
            IFormFile file,
            string? folder = null,
            string? publicId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "File is empty or null."
                    };
                }

                // Validate file size (20MB limit for general files)
                const int maxFileSize = 20 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "File size exceeds 20MB limit."
                    };
                }

                using var stream = file.OpenReadStream();
                
                // Generate object name
                var extension = Path.GetExtension(file.FileName);
                var objectName = publicId ?? $"{Guid.NewGuid()}{extension}";
                
                // Add folder prefix if specified
                if (!string.IsNullOrEmpty(folder))
                {
                    objectName = $"{folder.TrimEnd('/')}/{objectName}";
                }

                await _storageClient.UploadObjectAsync(_bucketName, objectName, null, stream);
                
                var url = $"https://storage.googleapis.com/{_bucketName}/{objectName}";
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "File uploaded successfully.",
                    Data = new { Url = url }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to upload file: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Upload resume with specific settings (to resumes folder, with user ID prefix)
        /// </summary>
        public async Task<ServiceResponse> UploadUserResumeAsync(
            int userId,
            IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var publicId = $"resumes/{userId}_{DateTime.UtcNow.Ticks}{extension}";
            return await UploadResumeAsync(file, null, publicId);
        }
    }
}

