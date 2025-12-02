using Data.Entities;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class JobEmploymentTypeRepository : IJobEmploymentTypeRepository
    {
        private readonly AICESDbContext _context;

        public JobEmploymentTypeRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(List<JobEmploymentType> jobEmploymentTypes)
        {
            await _context.JobEmploymentTypes.AddRangeAsync(jobEmploymentTypes);
        }

        public async Task DeleteByJobIdAsync(int jobId)
        {
            var toRemove = _context.JobEmploymentTypes.Where(x => x.JobId == jobId).ToList();
            if (toRemove.Count > 0)
            {
                _context.JobEmploymentTypes.RemoveRange(toRemove);
            }
        }
    }
}
