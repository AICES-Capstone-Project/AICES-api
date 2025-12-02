using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Hubs;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IUnitOfWork uow, IHubContext<NotificationHub> hubContext)
        {
            _uow = uow;
            _hubContext = hubContext;
        }

        public async Task<ServiceResponse> CreateAsync(int userId, NotificationTypeEnum type, string message, string? detail = null)
        {
            return await CreateWithInvitationAsync(userId, type, message, detail, null);
        }

        public async Task<ServiceResponse> CreateWithInvitationAsync(int userId, NotificationTypeEnum type, string message, string? detail = null, int? invitationId = null)
        {
            var notifRepo = _uow.GetRepository<INotificationRepository>();
            var notif = new Notification
            {
                UserId = userId,
                Type = type,
                Message = message,
                Detail = detail,
                InvitationId = invitationId
            };

            await notifRepo.AddAsync(notif);
            await _uow.SaveChangesAsync();

            // 🔔 Gửi realtime tới user
            await _hubContext.Clients.Group($"user-{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    notif.NotifId,
                    notif.Message,
                    notif.Detail,
                    notif.Type,
                    notif.InvitationId,
                    notif.CreatedAt
                });

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Notification sent successfully."
            };
        }

        public async Task<ServiceResponse> GetByUserIdAsync(int userId)
        {
            var notifRepo = _uow.GetRepository<INotificationRepository>();
            var notifs = await notifRepo.GetByUserIdWithInvitationAsync(userId);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Notifications retrieved successfully.",
                Data = notifs.Select(n => new NotificationWithInvitationResponse
                {
                    NotifId = n.NotifId,
                    Message = n.Message,
                    Detail = n.Detail,
                    Type = n.Type.ToString(),
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    Invitation = n.Invitation != null ? new InvitationDetailResponse
                    {
                        InvitationId = n.Invitation.InvitationId,
                        CompanyName = n.Invitation.Company?.Name ?? string.Empty,
                        SenderName = n.Invitation.Sender?.Profile?.FullName ?? n.Invitation.Sender?.Email ?? string.Empty,
                        Status = n.Invitation.InvitationStatus
                    } : null
                })
            };
        }

        public async Task<ServiceResponse> MarkAsReadAsync(ClaimsPrincipal user, int notifId)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(user);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid user ID format."
                };
            }

            var notifRepo = _uow.GetRepository<INotificationRepository>();
            var notif = await notifRepo.GetForUpdateAsync(notifId);

            if (notif == null || notif.UserId != userId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Notification not found or access denied."
                };
            }

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await notifRepo.UpdateAsync(notif);
                await _uow.SaveChangesAsync();
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Notification marked as read."
            };
        }

        public async Task<ServiceResponse> MarkAllAsReadAsync(ClaimsPrincipal user)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(user);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid user ID format."
                };
            }

            var notifRepo = _uow.GetRepository<INotificationRepository>();
            await notifRepo.MarkAllAsReadByUserIdAsync(userId);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "All notifications marked as read."
            };
        }
        public async Task<ServiceResponse> GetMyNotificationsAsync(ClaimsPrincipal user)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(user);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid user ID format."
                };
            }

            return await GetByUserIdAsync(userId);
        }

        public async Task<ServiceResponse> GetByIdAndMarkAsReadAsync(int userId, int notifId)
        {
            var notifRepo = _uow.GetRepository<INotificationRepository>();
            var notif = await notifRepo.GetForUpdateAsync(notifId);

            if (notif == null || notif.UserId != userId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Notification not found or access denied."
                };
            }

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await notifRepo.UpdateAsync(notif);
                await _uow.SaveChangesAsync();
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Notification retrieved successfully.",
                Data = new
                {
                    notif.NotifId,
                    notif.Message,
                    notif.Detail,
                    Type = notif.Type.ToString(),
                    notif.IsRead,
                    notif.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> GetNotificationDetailAsync(ClaimsPrincipal user, int notifId)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(user);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid user ID format."
                };
            }

            return await GetByIdAndMarkAsReadAsync(userId, notifId);
        }
    }
}