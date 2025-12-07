using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BusinessObjectLayer.Hubs
{
    public class ResumeHub : Hub
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
            Console.WriteLine($"✅ User {userId} connected to ResumeHub");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserIdFromClaims();

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
                Console.WriteLine($"❌ User {userId} disconnected from ResumeHub");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a job group to receive real-time updates for that job's resumes
        /// </summary>
        public async Task JoinJobGroup(int jobId)
        {
            var userId = GetUserIdFromClaims();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
            await Clients.Caller.SendAsync("JoinedJobGroup", $"✅ Joined job {jobId} group");
            Console.WriteLine($"✅ User {userId} joined job-{jobId} group");
        }

        /// <summary>
        /// Leave a job group
        /// </summary>
        public async Task LeaveJobGroup(int jobId)
        {
            var userId = GetUserIdFromClaims();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
            await Clients.Caller.SendAsync("LeftJobGroup", $"✅ Left job {jobId} group");
            Console.WriteLine($"✅ User {userId} left job-{jobId} group");
        }

        private string? GetUserIdFromClaims()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst("nameidentifier")?.Value
                ?? Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        }
    }
}
