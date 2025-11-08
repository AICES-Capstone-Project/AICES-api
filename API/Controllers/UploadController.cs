using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IGoogleCloudStorageService _storageService;

        public UploadController(IGoogleCloudStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                var errorResponse = new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "No file uploaded or file is empty."
                };
                return ControllerResponse.Response(errorResponse);
            }

            try
            {
                var url = await _storageService.UploadFileAsync(file);
                
                var successResponse = new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "File uploaded successfully.",
                    Data = new { url }
                };
                
                return ControllerResponse.Response(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to upload file: {ex.Message}"
                };
                return ControllerResponse.Response(errorResponse);
            }
        }
    }
}


