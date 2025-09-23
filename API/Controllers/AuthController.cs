using BusinessObjectLayer.IServices;
using Data.Models.Response;
using Data.Models.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
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
            try
            {
                var result = await _authService.RegisterAsync(request.Email, request.Password, 0);
                return Ok("Registration successful.");
            }
            catch (Exception ex)
            {
                return BadRequest("Email already exists.");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request.Email, request.Password);
                return Ok(new AuthResponse
                {
                    Token = response.Token,
                    UserId = response.UserId,
                    RoleName = response.RoleName
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new AuthResponse { Message = ex.Message });
            }
        }
    }
}
