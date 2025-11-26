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
                .Where(n => n.UserId == userId)
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

        public async Task<Notification?> GetByIdAsync(int notifId)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotifId == notifId);
        }

        public async Task UpdateAsync(Notification notification)
        {
            _context.Notifications.Update(notification);
        }

    }
}
