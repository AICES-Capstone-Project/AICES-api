using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IPaymentRepository
    {
        Task<Payment> AddAsync(Payment payment);
        Task<Payment?> GetByIdAsync(int id);
        Task<Payment?> GetForUpdateAsync(int id);
        Task UpdateAsync(Payment payment);
        Task<List<Payment>> GetPaymentHistoryByCompanyAsync(int companyId, int page, int pageSize);
        Task<int> GetTotalPaymentsByCompanyAsync(int companyId);
        Task<Payment?> GetLatestPendingByCompanyAsync(int companyId);
        Task<List<Payment>> GetPendingBeforeAsync(DateTime cutoff);
        Task<List<Payment>> GetPaymentsByCompanyAsync(int companyId);
        Task<Payment?> GetPaymentDetailByIdAsync(int paymentId, int companyId);

    }



}
