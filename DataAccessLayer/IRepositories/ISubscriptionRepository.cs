using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ISubscriptionRepository
    {
        Task<IEnumerable<Subscription>> GetAllAsync();
        Task<List<Subscription>> GetSubscriptionsAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalSubscriptionsAsync(string? search = null);
        Task<Subscription?> GetByIdAsync(int id);
        Task<Subscription> AddAsync(Subscription subscription);
        Task UpdateAsync(Subscription subscription);
        Task SoftDeleteAsync(Subscription subscription);
        Task<bool> ExistsByNameAsync(string name);
    }
}
