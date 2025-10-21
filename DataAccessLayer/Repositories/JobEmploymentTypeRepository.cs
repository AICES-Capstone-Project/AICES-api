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

        public async Task AddJobEmploymentTypesAsync(List<JobEmploymentType> jobEmploymentTypes)
        {
            _context.JobEmploymentTypes.AddRange(jobEmploymentTypes);
            await _context.SaveChangesAsync();
        }
    }
}
