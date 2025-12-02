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
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AICESDbContext _context;

        public SubscriptionRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Subscription>> GetAllAsync()
        {
            var query = _context.Subscriptions
                .AsNoTracking()
                .AsQueryable()
                .Where(s => s.IsActive);
            return await query.ToListAsync();
        }
        public async Task<List<Subscription>> GetSubscriptionsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Subscriptions
                .AsNoTracking()
                .AsQueryable()
                .Where(s => s.IsActive);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search) ||
                                       (s.Description != null && s.Description.Contains(search)));
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string? search = null)
        {
            var query = _context.Subscriptions
                .AsNoTracking()
                .AsQueryable()
                .Where(s => s.IsActive);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search) ||
                                       (s.Description != null && s.Description.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<Subscription?> GetByIdAsync(int id)
        {
            var query = _context.Subscriptions
                .AsNoTracking()
                .AsQueryable()
                .Where(s => s.IsActive);
            return await query.FirstOrDefaultAsync(s => s.SubscriptionId == id);
        }

        public async Task<Subscription?> GetByIdForUpdateAsync(int id)
        {
            return await _context.Subscriptions
                .Where(s => s.IsActive)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);
        }

        public async Task<Subscription> AddAsync(Subscription subscription)
        {
            await _context.Subscriptions.AddAsync(subscription);
            return subscription;
        }

        public async Task UpdateAsync(Subscription subscription)
        {
            _context.Subscriptions.Update(subscription);
        }

        public async Task SoftDeleteAsync(Subscription subscription)
        {
            subscription.IsActive = false;
            _context.Subscriptions.Update(subscription);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(s => s.Name == name);
        }
    }
}
