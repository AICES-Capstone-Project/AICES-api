using API.Common;
using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyNotifications()
        {
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("nameidentifier")?.Value
                      ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new
                {
                    status = "Unauthorized",
                    message = "User not authenticated."
                });
            }

            int userId = int.Parse(userIdClaim);

            var response = await _notificationService.GetByUserIdAsync(userId);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            var response = await _notificationService.GetByUserIdAsync(userId);
            return ControllerResponse.Response(response);
        }

        [HttpPost("mark-as-read/{notifId}")]
        public async Task<IActionResult> MarkAsRead(int notifId)
        {
            var response = await _notificationService.MarkAsReadAsync(notifId);
            return ControllerResponse.Response(response);
        }

        [HttpGet("detail/{notifId}")]
        public async Task<IActionResult> GetNotificationDetail(int notifId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("nameidentifier")?.Value
                              ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new
                {
                    status = "Unauthorized",
                    message = "User not authenticated."
                });
            }

            int userId = int.Parse(userIdClaim);

            var response = await _notificationService.GetByIdAndMarkAsReadAsync(userId, notifId);
            return ControllerResponse.Response(response);
        }

    }
}
