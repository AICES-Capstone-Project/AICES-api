using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class ComparisonRepository : IComparisonRepository
    {
        private readonly AICESDbContext _context;

        public ComparisonRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Comparison> CreateAsync(Comparison comparison)
        {
            await _context.Comparisons.AddAsync(comparison);
            return comparison;
        }

        public async Task<Comparison?> GetByIdAsync(int comparisonId)
        {
            return await _context.Comparisons
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComparisonId == comparisonId && c.IsActive);
        }

        public async Task<Comparison?> GetByIdWithApplicationsAsync(int comparisonId)
        {
            return await _context.Comparisons
                .AsNoTracking()
                .Include(c => c.ApplicationComparisons)
                    .ThenInclude(ac => ac.ResumeApplication)
                        .ThenInclude(ra => ra.Resume)
                .FirstOrDefaultAsync(c => c.ComparisonId == comparisonId && c.IsActive);
        }

        public async Task<List<Comparison>> GetByJobIdAndCampaignIdAsync(int jobId, int campaignId)
        {
            return await _context.Comparisons
                .AsNoTracking()
                .Where(c => c.JobId == jobId 
                            && c.CampaignId == campaignId 
                            && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(Comparison comparison)
        {
            _context.Comparisons.Update(comparison);
        }
    }
}
