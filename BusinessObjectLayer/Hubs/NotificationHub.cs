using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BusinessObjectLayer.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdFromClaims();

            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Connected", "⚠️ Invalid or missing token.");
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            await Clients.Caller.SendAsync("Connected", $"✅ Connected as user {userId}");
            Console.WriteLine($"✅ User {userId} joined group user-{userId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserIdFromClaims();

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
                Console.WriteLine($"❌ User {userId} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
        }

        private string? GetUserIdFromClaims()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst("nameidentifier")?.Value
                ?? Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        }
    }
}

