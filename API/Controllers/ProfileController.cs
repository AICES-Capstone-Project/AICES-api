using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
            var serviceResponse = await _profileService.UpdateProfileFromClaimsAsync(User, request);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}
