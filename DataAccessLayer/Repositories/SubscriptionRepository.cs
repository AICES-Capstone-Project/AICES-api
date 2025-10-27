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
            return await _context.Subscriptions
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subscription>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.Subscriptions.AsQueryable();

            if (!includeInactive)
                query = query.Where(s => s.IsActive); 

            return await query.ToListAsync();
        }



        public async Task<Subscription?> GetByIdAsync(int id)
        {
            return await _context.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == id && s.IsActive);
        }

        public async Task<Subscription?> GetByIdAsync(int id, bool includeInactive)
        {
            var query = _context.Subscriptions.AsQueryable();

            if (!includeInactive)
                query = query.Where(s => s.IsActive);

            return await query.FirstOrDefaultAsync(s => s.SubscriptionId == id);
        }

        public async Task<Subscription> AddAsync(Subscription subscription)
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task UpdateAsync(Subscription subscription)
        {
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Subscription subscription)
        {
            subscription.IsActive = false;
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Subscriptions.AnyAsync(s => s.Name == name);
        }
    }
}
