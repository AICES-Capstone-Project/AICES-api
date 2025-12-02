using API.Common;
using BusinessObjectLayer.IServices.Auth;
using Data.Models.Request;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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

            // Set refresh token as HTTP-only cookie and return only access token
            if (serviceResponse.Status == Data.Enum.SRStatus.Success && serviceResponse.Data is AuthTokenResponse tokens)
            {
                SetRefreshTokenCookie(tokens.RefreshToken);
                
                // Replace with LoginResponse (only accessToken)
                serviceResponse.Data = new LoginResponse { AccessToken = tokens.AccessToken };
            }

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
            var serviceResponse = await _authService.GoogleLoginAsync(request.AccessToken);

            // Set refresh token as HTTP-only cookie and return only access token
            if (serviceResponse.Status == Data.Enum.SRStatus.Success && serviceResponse.Data is AuthTokenResponse tokens)
            {
                SetRefreshTokenCookie(tokens.RefreshToken);
                
                // Replace with LoginResponse (only accessToken)
                serviceResponse.Data = new LoginResponse { AccessToken = tokens.AccessToken };
            }

            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("github")]
        public async Task<IActionResult> GitHubLogin([FromBody] GitHubLoginRequest request)
        {
            var serviceResponse = await _authService.GitHubLoginAsync(request.Code);

            if (serviceResponse.Status == Data.Enum.SRStatus.Success && serviceResponse.Data is AuthTokenResponse tokens)
            {
                SetRefreshTokenCookie(tokens.RefreshToken);
                serviceResponse.Data = new LoginResponse { AccessToken = tokens.AccessToken };
            }

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
        public async Task<IActionResult> RefreshToken()
        {
            // Get refresh token from cookie
            var refreshToken = Request.Cookies["refreshToken"];
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                var errorResponse = new ServiceResponse
                {
                    Status = Data.Enum.SRStatus.Unauthorized,
                    Message = "Refresh token not found."
                };
                return ControllerResponse.Response(errorResponse);
            }

            var serviceResponse = await _authService.RefreshTokenAsync(refreshToken);

            // Set new refresh token as HTTP-only cookie and return only access token
            if (serviceResponse.Status == Data.Enum.SRStatus.Success && serviceResponse.Data is AuthTokenResponse tokens)
            {
                SetRefreshTokenCookie(tokens.RefreshToken);
                
                // Replace with LoginResponse (only accessToken)
                serviceResponse.Data = new LoginResponse { AccessToken = tokens.AccessToken };
            }

            return ControllerResponse.Response(serviceResponse);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Get refresh token from cookie
            var refreshToken = Request.Cookies["refreshToken"];
            
            ServiceResponse serviceResponse;
            
            if (!string.IsNullOrEmpty(refreshToken))
            {
                serviceResponse = await _authService.LogoutAsync(refreshToken);
            }
            else
            {
                serviceResponse = new ServiceResponse
                {
                    Status = Data.Enum.SRStatus.NotFound,
                    Message = "Already logged out."
                };
            }

            // Clear the refresh token cookie (must use same options as when setting)
            ClearRefreshTokenCookie();

            return ControllerResponse.Response(serviceResponse);
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var serviceResponse = await _authService.ChangePasswordAsync(User, request.OldPassword, request.NewPassword);
            return ControllerResponse.Response(serviceResponse);
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,              
                Secure = true, // Required when SameSite=None (and you're using HTTPS in dev)
                SameSite = SameSiteMode.None, // Required for cross-origin requests
                Expires = DateTimeOffset.UtcNow.AddDays(7) 
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        private void ClearRefreshTokenCookie()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(-1) // Expire immediately
            };

            Response.Cookies.Append("refreshToken", "", cookieOptions);
        }
    }
}
