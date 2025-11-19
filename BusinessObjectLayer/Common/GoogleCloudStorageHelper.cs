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

        public GoogleCloudStorageHelper(string bucketName)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));

            var credential = GoogleCredential.GetApplicationDefault();
            Console.WriteLine("🔐 Using Google ADC (Workload Identity / Cloud Run SA)");
            _storageClient = StorageClient.Create(credential);
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
                
                var url = $"gs://{_bucketName}/{objectName}";
                
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume uploaded successfully.",
                    Data = new { Url = url }
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
                
                var url = $"gs://{_bucketName}/{objectName}";
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

