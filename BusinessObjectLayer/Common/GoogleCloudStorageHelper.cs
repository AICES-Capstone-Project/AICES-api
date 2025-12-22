using Data.Enum;
using Data.Models.Response;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Iam.Credentials.V1;
using Google.Cloud.Storage.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public class GoogleCloudStorageHelper : IStorageHelper
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly GoogleCredential _credential;

        public GoogleCloudStorageHelper(string bucketName, string serviceAccountEmail)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));

            _credential = GoogleCredential.GetApplicationDefault();
            _storageClient = StorageClient.Create(_credential);
        }

        // ----------------------------
        // Upload File
        // ----------------------------
        public async Task<ServiceResponse> UploadFileAsync(
            IFormFile file,
            string? folder = null,
            string? publicId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "File is empty."
                    };

                using var stream = file.OpenReadStream();

                var extension = Path.GetExtension(file.FileName);
                var objectName = publicId ?? $"{Guid.NewGuid()}{extension}";

                if (!string.IsNullOrEmpty(folder))
                    objectName = $"{folder.TrimEnd('/')}/{objectName}";

                await _storageClient.UploadObjectAsync(
                    bucket: _bucketName,
                    objectName: objectName,
                    contentType: file.ContentType,
                    source: stream
                );

                // SIGNED URL
                var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "File uploaded successfully.",
                    Data = new
                    {
                        Url = publicUrl,
                        ObjectName = objectName,
                        BucketName = _bucketName
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Upload failed: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> DeleteFileAsync(string publicId)
        {
            try
            {
                if (string.IsNullOrEmpty(publicId))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "PublicId is required."
                    };
                }

                await _storageClient.DeleteObjectAsync(_bucketName, publicId);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "File deleted successfully."
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Delete failed: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetSignedUrlAsync(string publicId, int expirationMinutes = 60)
        {
            try
            {
                if (string.IsNullOrEmpty(publicId))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "PublicId is required."
                    };
                }

                // Create a signed URL for the object
                var urlSigner = UrlSigner.FromCredential(_credential);
                var url = await urlSigner.SignAsync(
                    _bucketName,
                    publicId,
                    TimeSpan.FromMinutes(expirationMinutes),
                    HttpMethod.Get);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Signed URL generated successfully.",
                    Data = url
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to generate signed URL: {ex.Message}"
                };
            }
        }
    }
}