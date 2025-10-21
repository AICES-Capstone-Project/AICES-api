using Data.Enum;
using Data.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace API.Common
{
    public static class ControllerResponse
    {
        public static IActionResult Response(ServiceResponse serviceResponse)
        {
            var result = new ObjectResult(serviceResponse)
            {
                StatusCode = serviceResponse.Status switch
                {
                    SRStatus.Success => StatusCodes.Status200OK,
                    SRStatus.NotFound => StatusCodes.Status404NotFound,
                    SRStatus.Error => StatusCodes.Status500InternalServerError,
                    SRStatus.Duplicated => StatusCodes.Status400BadRequest,
                    SRStatus.Unauthorized => StatusCodes.Status401Unauthorized,
                    SRStatus.Forbidden => StatusCodes.Status403Forbidden,
                    SRStatus.Validation => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError
                }
            };

            return result;
        }
    }
}