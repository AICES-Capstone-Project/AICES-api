using System.Security.Claims;
using System.Threading.Tasks;
using BusinessObjectLayer.Common;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;

namespace API.Middleware
{
    
    public class SingleSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public SingleSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAuthRepository authRepository)
        {
            var user = context.User;

            // Chỉ kiểm tra nếu đã auth (JWT hợp lệ)
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userIdString = ClaimUtils.GetUserIdClaim(user);
                var sessionIdClaim = user.FindFirst("sid")?.Value;

                if (!string.IsNullOrEmpty(userIdString)
                    && int.TryParse(userIdString, out var userId)
                    && !string.IsNullOrEmpty(sessionIdClaim))
                {
                    var dbUser = await authRepository.GetByIdNoTrackingAsync(userId);

                    if (dbUser == null || !dbUser.IsActive ||
                        string.IsNullOrEmpty(dbUser.CurrentSessionId) ||
                        !string.Equals(dbUser.CurrentSessionId, sessionIdClaim, System.StringComparison.OrdinalIgnoreCase) ||
                        !dbUser.CurrentSessionExpiry.HasValue ||
                        dbUser.CurrentSessionExpiry.Value < System.DateTime.UtcNow)
                    {
                        
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Your account are logged in another device please login agian.");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}


