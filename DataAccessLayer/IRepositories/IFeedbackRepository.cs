using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IFeedbackRepository
    {
        Task<Feedback> AddAsync(Feedback feedback);
        Task<Feedback?> GetByIdAsync(int feedbackId);
        Task<Feedback?> GetForUpdateAsync(int feedbackId);
        Task<List<Feedback>> GetFeedbacksByComUserIdAsync(int comUserId, int page = 1, int pageSize = 10);
        Task<int> GetTotalFeedbacksByComUserIdAsync(int comUserId);
        Task<List<Feedback>> GetAllFeedbacksAsync(int page = 1, int pageSize = 10);
        Task<int> GetTotalFeedbacksAsync();
        Task UpdateAsync(Feedback feedback);
        Task SoftDeleteAsync(Feedback feedback);
    }
}
