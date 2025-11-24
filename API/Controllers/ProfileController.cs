using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/auth/profile")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpPatch]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errorResponse = new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Validation failed.",
                    Data = ModelState
                };
                return ControllerResponse.Response(errorResponse);
            }

            var userId = GetUserIdFromClaims();
            if (userId == 0)
            {
                var errorResponse = new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid token."
                };
                return ControllerResponse.Response(errorResponse);
            }

            var serviceResponse = await _profileService.UpdateProfileAsync(userId, request);
            return ControllerResponse.Response(serviceResponse);
        }

        private int GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
