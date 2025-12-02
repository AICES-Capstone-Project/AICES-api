using Data.Enum;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface INotificationService
    {
        Task<ServiceResponse> CreateAsync(int userId, NotificationTypeEnum type, string message, string? detail = null);
        Task<ServiceResponse> GetByUserIdAsync(int userId);
        Task<ServiceResponse> GetCurrentUserListAsync(ClaimsPrincipal user);
        Task<ServiceResponse> MarkAsReadAsync(ClaimsPrincipal user, int notifId);
        Task<ServiceResponse> MarkAllAsReadAsync(ClaimsPrincipal user);
        Task<ServiceResponse> GetByIdAndMarkAsReadAsync(int userId, int notifId);
        Task<ServiceResponse> GetNotificationDetailAsync(ClaimsPrincipal user, int notifId);
    }
}
