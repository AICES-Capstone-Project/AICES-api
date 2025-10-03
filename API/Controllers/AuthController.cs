using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
            var serviceResponse = await _authService.RegisterAsync(request.Email, request.Password, request.FullName); 
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var serviceResponse = await _authService.LoginAsync(request.Email, request.Password);

            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            var serviceResponse = await _authService.VerifyEmailAsync(token);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var serviceResponse = await _authService.GoogleLoginAsync(request.IdToken);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("request-password-reset")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request)
        {
            var serviceResponse = await _authService.RequestPasswordResetAsync(request.Email);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var serviceResponse = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var serviceResponse = await _authService.GetCurrentUserInfoAsync(User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var serviceResponse = await _authService.RefreshTokenAsync(request.RefreshToken);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var serviceResponse = await _authService.LogoutAsync(request.RefreshToken);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}
