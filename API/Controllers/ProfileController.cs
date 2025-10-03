using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu JWT
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpPatch("update")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Unauthorized(new ServiceResponse
                {
                    Status = Data.Enum.SRStatus.Unauthorized,
                    Message = "Invalid token."
                });
            }

            var serviceResponse = await _profileService.UpdateProfileAsync(userId, request);
            return Ok(serviceResponse); // Trả về JSON trực tiếp
        }
    }
}
