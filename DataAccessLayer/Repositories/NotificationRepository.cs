using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly AICESDbContext _context;

        public NotificationRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Notification notification)
        {
            await _context.Notifications.AddAsync(notification);
        }

        public async Task<IEnumerable<Notification>> GetByUserIdAsync(int userId)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.IsActive && n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Notification>> GetByUserIdWithInvitationAsync(int userId)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Include(n => n.Invitation)
                    .ThenInclude(i => i!.Sender)
                        .ThenInclude(s => s.Profile)
                .Include(n => n.Invitation)
                    .ThenInclude(i => i!.Company)
                .Where(n => n.IsActive && n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notifId)
        {
            var notif = await _context.Notifications.FindAsync(notifId);
            if (notif != null)
            {
                notif.IsRead = true;
                _context.Notifications.Update(notif);
            }
        }

        public async Task MarkAllAsReadByUserIdAsync(int userId)
        {
            var notifs = await _context.Notifications
                .Where(n => n.IsActive && n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (notifs.Count > 0)
            {
                foreach (var notif in notifs)
                {
                    notif.IsRead = true;
                }

                _context.Notifications.UpdateRange(notifs);
            }
        }

        public async Task<Notification?> GetByIdAsync(int notifId)
        {
            return await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.IsActive && n.NotifId == notifId);
        }

        public async Task<Notification?> GetForUpdateAsync(int notifId)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.IsActive && n.NotifId == notifId);
        }

        public async Task UpdateAsync(Notification notification)
        {
            _context.Notifications.Update(notification);
        }

    }
}
