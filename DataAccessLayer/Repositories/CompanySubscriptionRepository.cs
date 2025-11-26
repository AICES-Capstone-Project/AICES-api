using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class CompanySubscriptionRepository : ICompanySubscriptionRepository
    {
        private readonly AICESDbContext _context;

        public CompanySubscriptionRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<CompanySubscription>> GetCompanySubscriptionsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.CompanySubscriptions
                .AsNoTracking()
                .Include(cs => cs.Company)
                .Include(cs => cs.Subscription)
                .Where(cs => cs.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(cs => 
                    cs.Company.Name.Contains(search) ||
                    cs.Subscription.Name.Contains(search));
            }

            return await query
                .OrderByDescending(cs => cs.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCompanySubscriptionsAsync(string? search = null)
        {
            var query = _context.CompanySubscriptions
                .AsNoTracking()
                .Include(cs => cs.Company)
                .Include(cs => cs.Subscription)
                .Where(cs => cs.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(cs => 
                    cs.Company.Name.Contains(search) ||
                    cs.Subscription.Name.Contains(search));
            }

            return await query.CountAsync();
        }

        public async Task<CompanySubscription?> GetByIdAsync(int id)
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Include(cs => cs.Company)
                .Include(cs => cs.Subscription)
                .Where(cs => cs.ComSubId == id && cs.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySubscription?> GetForUpdateAsync(int id)
        {
            return await _context.CompanySubscriptions
                .Include(cs => cs.Company)
                .Include(cs => cs.Subscription)
                .Where(cs => cs.ComSubId == id && cs.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySubscription?> GetActiveSubscriptionAsync(int companyId, int subscriptionId)
        {
            var now = DateTime.UtcNow;
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Where(cs => cs.CompanyId == companyId
                    && cs.SubscriptionId == subscriptionId
                    && cs.SubscriptionStatus == SubscriptionStatusEnum.Active
                    && cs.IsActive
                    && cs.EndDate > now)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySubscription?> GetAnyActiveSubscriptionByCompanyAsync(int companyId)
        {
            var now = DateTime.UtcNow;
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Include(cs => cs.Subscription)
                .Where(cs => cs.CompanyId == companyId
                    && (cs.SubscriptionStatus == SubscriptionStatusEnum.Active || cs.SubscriptionStatus == SubscriptionStatusEnum.Pending)
                    && cs.IsActive
                    && cs.EndDate > now)
                .FirstOrDefaultAsync();
        }

        public async Task<CompanySubscription> AddAsync(CompanySubscription companySubscription)
        {
            await _context.CompanySubscriptions.AddAsync(companySubscription);
            return companySubscription;
        }

        public async Task UpdateAsync(CompanySubscription companySubscription)
        {
            _context.CompanySubscriptions.Update(companySubscription);
        }

        public async Task SoftDeleteAsync(CompanySubscription companySubscription)
        {
            companySubscription.IsActive = false;
            _context.CompanySubscriptions.Update(companySubscription);
        }

        public async Task<CompanySubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId)
        {
            return await _context.CompanySubscriptions
                .AsNoTracking()
                .Include(cs => cs.Company)
                .Include(cs => cs.Subscription)
                .Where(cs => cs.IsActive && cs.StripeSubscriptionId == stripeSubscriptionId)
                .FirstOrDefaultAsync();
        }

    }
}

