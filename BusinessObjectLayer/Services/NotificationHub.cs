using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace BusinessObjectLayer.Services
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // Thử lấy userId từ nhiều kiểu claim khác nhau
            var userId =
                Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                Context.User?.FindFirst("nameidentifier")?.Value ??
                Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                await Clients.Caller.SendAsync("Connected", $"✅ Connected as user {userId}");
                Console.WriteLine($"✅ User {userId} joined group user-{userId}");
            }
            else
            {
                await Clients.Caller.SendAsync("Connected", "⚠️ Connected, but user ID not found in token.");
                Console.WriteLine("⚠️ Could not retrieve user ID from claims.");
            }
        }
    }
}