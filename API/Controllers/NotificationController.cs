using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
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
            var response = await _notificationService.GetMyNotificationsAsync(User);
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
            var response = await _notificationService.GetNotificationDetailAsync(User, notifId);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Test endpoint: Gửi thông báo cho một userId cụ thể để test SignalR
        /// </summary>
        [HttpPost("test/send")]
        // [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> SendTestNotification([FromBody] NotificationRequest request)
        {
            var response = await _notificationService.CreateAsync(
                request.UserId,
                request.Type,
                request.Message,
                request.Detail
            );
            return ControllerResponse.Response(response);
        }
    }
}
