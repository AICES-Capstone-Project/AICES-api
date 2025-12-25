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
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly AICESDbContext _context;

        public FeedbackRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Feedback> AddAsync(Feedback feedback)
        {
            await _context.Feedbacks.AddAsync(feedback);
            return feedback;
        }

        public async Task<Feedback?> GetByIdAsync(int feedbackId)
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .FirstOrDefaultAsync(f => f.IsActive && f.FeedbackId == feedbackId);
        }

        public async Task<Feedback?> GetForUpdateAsync(int feedbackId)
        {
            return await _context.Feedbacks
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .FirstOrDefaultAsync(f => f.IsActive && f.FeedbackId == feedbackId);
        }

        public async Task<List<Feedback>> GetFeedbacksByComUserIdAsync(int comUserId, int page = 1, int pageSize = 10)
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .Where(f => f.IsActive && f.ComUserId == comUserId)
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalFeedbacksByComUserIdAsync(int comUserId)
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .CountAsync(f => f.IsActive && f.ComUserId == comUserId);
        }

        public async Task<List<Feedback>> GetAllFeedbacksAsync(int page = 1, int pageSize = 10)
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(f => f.CompanyUser)
                    .ThenInclude(cu => cu.Company)
                .Where(f => f.IsActive)
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalFeedbacksAsync()
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .CountAsync(f => f.IsActive);
        }

        public async Task<bool> HasRecentFeedbackAsync(int comUserId, DateTime sinceDate)
        {
            return await _context.Feedbacks
                .AsNoTracking()
                .AnyAsync(f => f.IsActive && f.ComUserId == comUserId && f.CreatedAt >= sinceDate);
        }

        public async Task UpdateAsync(Feedback feedback)
        {
            _context.Feedbacks.Update(feedback);
        }

        public async Task SoftDeleteAsync(Feedback feedback)
        {
            feedback.IsActive = false;
            _context.Feedbacks.Update(feedback);
        }
    }
}
