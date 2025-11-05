using Data.Enum;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface INotificationService
    {
        Task<ServiceResponse> CreateAsync(int userId, NotificationTypeEnum type, string message, string? detail = null);
        Task<ServiceResponse> GetByUserIdAsync(int userId);
        Task<ServiceResponse> MarkAsReadAsync(int notifId);
        Task<ServiceResponse> GetByIdAndMarkAsReadAsync(int userId, int notifId);
    }
}
