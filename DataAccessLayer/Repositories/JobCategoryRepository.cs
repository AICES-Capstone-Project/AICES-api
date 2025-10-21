using Data.Entities;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class JobCategoryRepository : IJobCategoryRepository
    {
        private readonly AICESDbContext _context;

        public JobCategoryRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddJobCategoriesAsync(List<JobCategory> jobCategories)
        {
            _context.JobCategories.AddRange(jobCategories);
            await _context.SaveChangesAsync();
        }
    }
}
