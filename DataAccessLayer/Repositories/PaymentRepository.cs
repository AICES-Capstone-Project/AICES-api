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
            await _context.Payments.AddAsync(payment);
            return payment;
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            return await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsActive && p.PaymentId == id);
        }

        public async Task<Payment?> GetForUpdateAsync(int id)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.IsActive && p.PaymentId == id);
        }

        public async Task UpdateAsync(Payment payment)
        {
            _context.Payments.Update(payment);
        }

        public async Task<List<Payment>> GetPaymentHistoryByCompanyAsync(int companyId, int page, int pageSize)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.IsActive && p.CompanyId == companyId)
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
                .AsNoTracking()
                .Where(p => p.IsActive && p.CompanyId == companyId)
                .CountAsync();
        }

        public async Task<Payment?> GetLatestPendingByCompanyAsync(int companyId)
        {
            return await _context.Payments
                .Where(p => p.IsActive && p.CompanyId == companyId && p.PaymentStatus == PaymentStatusEnum.Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Payment>> GetPendingBeforeAsync(DateTime cutoff)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.IsActive && p.PaymentStatus == PaymentStatusEnum.Pending 
                    && p.IsActive 
                    && p.CreatedAt.HasValue 
                    && p.CreatedAt.Value < cutoff)
                .ToListAsync();
        }

        public async Task<List<Payment>> GetPaymentsByCompanyAsync(int companyId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.IsActive && p.CompanyId == companyId && (p.PaymentStatus == PaymentStatusEnum.Paid || p.PaymentStatus == PaymentStatusEnum.Canceled || p.PaymentStatus == PaymentStatusEnum.Refunded || p.PaymentStatus == PaymentStatusEnum.Failed))
                .Include(p => p.CompanySubscription)
                    .ThenInclude(cs => cs.Subscription)
                .Include(p => p.Transactions)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Payment?> GetPaymentDetailByIdAsync(int paymentId, int companyId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.IsActive && p.PaymentId == paymentId && p.CompanyId == companyId && (p.PaymentStatus == PaymentStatusEnum.Paid || p.PaymentStatus == PaymentStatusEnum.Canceled || p.PaymentStatus == PaymentStatusEnum.Refunded || p.PaymentStatus == PaymentStatusEnum.Failed))
                .Include(p => p.CompanySubscription)
                    .ThenInclude(cs => cs.Subscription)
                .Include(p => p.Transactions)
                .FirstOrDefaultAsync();
        }

        public async Task<Payment?> GetByIdWithTransactionsAsync(int paymentId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.IsActive && p.PaymentId == paymentId)
                .Include(p => p.Transactions)
                .FirstOrDefaultAsync();
        }

    }



}
