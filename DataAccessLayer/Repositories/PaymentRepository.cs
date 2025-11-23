using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly AICESDbContext _context;

        public PaymentRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Payment> AddAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == id);
        }

        public async Task UpdateAsync(Payment payment)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Payment>> GetPaymentHistoryByCompanyAsync(int companyId, int page, int pageSize)
        {
            return await _context.Payments
                .Where(p => p.CompanyId == companyId && p.IsActive)
                .Include(p => p.Transactions)
                .Include(p => p.Company)
                    .ThenInclude(c => c.CompanySubscriptions)
                        .ThenInclude(cs => cs.Subscription)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }


        public async Task<int> GetTotalPaymentsByCompanyAsync(int companyId)
        {
            return await _context.Payments
                .Where(p => p.CompanyId == companyId && p.IsActive)
                .CountAsync();
        }

        public async Task<Payment?> GetLatestPendingByCompanyAsync(int companyId)
        {
            return await _context.Payments
                .Where(p => p.CompanyId == companyId && p.PaymentStatus == PaymentStatusEnum.Pending && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Payment>> GetPendingBeforeAsync(DateTime cutoff)
        {
            return await _context.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.Pending 
                    && p.IsActive 
                    && p.CreatedAt.HasValue 
                    && p.CreatedAt.Value < cutoff)
                .ToListAsync();
        }

    }



}
