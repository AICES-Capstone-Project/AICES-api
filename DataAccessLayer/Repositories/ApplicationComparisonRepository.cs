using Data.Entities;
using DataAccessLayer.IRepositories;

namespace DataAccessLayer.Repositories
{
    public class ApplicationComparisonRepository : IApplicationComparisonRepository
    {
        private readonly AICESDbContext _context;

        public ApplicationComparisonRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<ApplicationComparison> CreateAsync(ApplicationComparison applicationComparison)
        {
            await _context.ApplicationComparisons.AddAsync(applicationComparison);
            return applicationComparison;
        }
    }
}
