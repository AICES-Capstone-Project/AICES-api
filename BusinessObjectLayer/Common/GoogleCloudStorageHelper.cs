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
    public class GoogleCloudStorageHelper
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        // Email service account used for signing
        private readonly string _serviceAccountEmail;

        public GoogleCloudStorageHelper(string bucketName, string serviceAccountEmail)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _serviceAccountEmail = serviceAccountEmail ?? throw new ArgumentNullException(nameof(serviceAccountEmail));

            var credential = GoogleCredential.GetApplicationDefault();
            _storageClient = StorageClient.Create(credential);
        }

        // ----------------------------
        // Upload Resume (you need this method back)
        // ----------------------------
        public async Task<ServiceResponse> UploadResumeAsync(IFormFile file)
        {
            return await UploadFileAsync(file, "resumes");
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
                var signedUrl = await GenerateSignedUrlAsync(objectName);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "File uploaded successfully.",
                    Data = new
                    {
                        Url = signedUrl,
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

        // ----------------------------
        // Generate Signed URL
        // ----------------------------
        public async Task<string> GenerateSignedUrlAsync(string objectName, TimeSpan? expiration = null)
        {
            var expires = expiration ?? TimeSpan.FromHours(1);
            var expireTime = DateTime.UtcNow.Add(expires);
            long expireTimestamp = expireTime.ToUnixSeconds();   // fixed

            string stringToSign =
                $"GET\n" +
                $"\n" +   // MD5
                $"\n" +   // Content-Type
                $"{expireTimestamp}\n" +
                $"/{_bucketName}/{objectName}";

            var iamClient = await IAMCredentialsClient.CreateAsync();

            var saResource = $"projects/-/serviceAccounts/{_serviceAccountEmail}";

            var request = new SignBlobRequest
            {
                Name = saResource,
                Payload = ByteString.CopyFromUtf8(stringToSign)
            };

            var response = await iamClient.SignBlobAsync(request);

            string signature = Convert.ToBase64String(response.SignedBlob.ToByteArray());

            string signedUrl =
                $"https://storage.googleapis.com/{_bucketName}/{objectName}" +
                $"?GoogleAccessId={_serviceAccountEmail}" +
                $"&Expires={expireTimestamp}" +
                $"&Signature={Uri.EscapeDataString(signature)}";

            return signedUrl;
        }
    }
}

// ----------------------------
// Extension MUST be OUTSIDE any class
// ----------------------------
public static class DateTimeExtensions
{
    public static long ToUnixSeconds(this DateTime dt)
    {
        return (long)Math.Floor((dt - DateTime.UnixEpoch).TotalSeconds);
    }
}
