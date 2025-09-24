using BusinessObjectLayer.IServices;
using Data.Models.Response;
using Data.Models.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using API.Common;

namespace API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var serviceResponse = await _authService.RegisterAsync(request.Email, request.Password, 0);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var serviceResponse = await _authService.LoginAsync(request.Email, request.Password);

            return ControllerResponse.Response(serviceResponse);
        }
    }
}
