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
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<List<Job>> GetJobsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Company != null && j.Company.Name != null && j.Company.Name.Contains(search)));
            }

            return await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalJobsAsync(string? search = null)
        {
            var query = _context.Jobs.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Company != null && j.Company.Name != null && j.Company.Name.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Job>> GetJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalJobsByCompanyIdAsync(int companyId, string? search = null)
        {
            var query = _context.Jobs
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<Job?> GetJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task UpdateJobAsync(Job job)
        {
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteJobAsync(Job job)
        {
            job.IsActive = false;
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();
        }
    }
}


