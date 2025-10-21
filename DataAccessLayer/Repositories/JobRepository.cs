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
    public class JobRepository : IJobRepository
    {
        private readonly AICESDbContext _context;

        public JobRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Job> CreateJobAsync(Job job)
        {
            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();
            return job;
        }

        public async Task<Job?> GetJobByIdAsync(int jobId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                .Include(j => j.JobCategories!)
                    .ThenInclude(jc => jc.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.Criteria)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }
    }
}


