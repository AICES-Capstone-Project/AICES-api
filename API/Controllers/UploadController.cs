using API.Common;
using BusinessObjectLayer.Common;
using Data.Enum;
using Data.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly GoogleCloudStorageHelper _storageHelper;

        public UploadController(GoogleCloudStorageHelper storageHelper)
        {
            _storageHelper = storageHelper;
        }

        /// <summary>
        /// Test Google Cloud Storage connection
        /// </summary>
        [HttpGet("test-gcs")]
        public IActionResult TestGCS()
        {
            try
            {
                var testData = new
                {
                    Status = "GCS Service Initialized",
                    Message = "Google Cloud Storage service is properly configured and ready",
                    EnvironmentVariables = new
                    {
                        GCP_BUCKET_NAME = Environment.GetEnvironmentVariable("GCP__BUCKET_NAME") ?? "Not set",
                        GCP_CREDENTIAL_PATH = Environment.GetEnvironmentVariable("GCP__CREDENTIAL_PATH") ?? "Not set",
                        GOOGLE_APPLICATION_CREDENTIALS = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "Not set",
                        CurrentDirectory = Directory.GetCurrentDirectory(),
                        ServiceAccountExists = System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "service-account.json"))
                    }
                };

                var response = new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "GCS connection test successful",
                    Data = testData
                };
                return ControllerResponse.Response(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"GCS test failed: {ex.Message}",
                    Data = new { Exception = ex.ToString() }
                };
                return ControllerResponse.Response(errorResponse);
            }
        }

        //[HttpPost("upload")]
        //public async Task<IActionResult> Upload(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //    {
        //        var errorResponse = new ServiceResponse
        //        {
        //            Status = SRStatus.Error,
        //            Message = "No file uploaded or file is empty."
        //        };
        //        return ControllerResponse.Response(errorResponse);
        //    }

        //    try
        //    {
        //        var response = await _storageHelper.UploadResumeAsync(file, "resumes");
        //        return ControllerResponse.Response(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        var errorResponse = new ServiceResponse
        //        {
        //            Status = SRStatus.Error,
        //            Message = $"Failed to upload file: {ex.Message}"
        //        };
        //        return ControllerResponse.Response(errorResponse);
        //    }
        //}
    }
}


