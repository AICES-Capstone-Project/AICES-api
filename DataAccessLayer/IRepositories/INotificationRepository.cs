using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface INotificationRepository
    {
        Task AddAsync(Notification notification);
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId);
        Task MarkAsReadAsync(int notifId);
        Task<Notification?> GetByIdAsync(int notifId);
        Task UpdateAsync(Notification notification);

    }
}
