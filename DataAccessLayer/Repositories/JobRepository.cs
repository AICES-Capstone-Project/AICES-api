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
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<List<Job>> GetPublishedJobsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.JobStatus == JobStatusEnum.Published && j.IsActive)
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

        public async Task<int> GetTotalPublishedJobsAsync(string? search = null)
        {
            var query = _context.Jobs.Where(j => j.JobStatus == JobStatusEnum.Published && j.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Company != null && j.Company.Name != null && j.Company.Name.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Job>> GetPublishedJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Published && j.IsActive)
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

        public async Task<int> GetTotalPublishedJobsByCompanyIdAsync(int companyId, string? search = null)
        {
            var query = _context.Jobs
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Published && j.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Job>> GetPendingJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Pending)
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

        public async Task<int> GetTotalPendingJobsByCompanyIdAsync(int companyId, string? search = null)
        {
            var query = _context.Jobs
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Pending && j.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<Job?> GetPublishedJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
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

         public async Task<Job?> GetPendingJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Pending)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<Job?> GetAllJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<List<Job>> GetJobsByComUserIdAsync(int comUserId, int page, int pageSize, string? search = null, JobStatusEnum? status = null)
        {
            var query = _context.Jobs
                .Include(j => j.Company)
                .Include(j => j.CompanyUser)
                    .ThenInclude(cu => cu.User)
                        .ThenInclude(u => u.Profile)
                .Include(j => j.Specialization!)
                    .ThenInclude(s => s.Category)
                .Include(j => j.JobEmploymentTypes!)
                    .ThenInclude(jet => jet.EmploymentType)
                .Include(j => j.JobSkills!)
                    .ThenInclude(js => js.Skill)
                .Include(j => j.Criteria)
                .Where(j => j.ComUserId == comUserId && j.IsActive)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(j => j.JobStatus == status.Value);
            }

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

        public async Task<int> GetTotalJobsByComUserIdAsync(int comUserId, string? search = null, JobStatusEnum? status = null)
        {
            var query = _context.Jobs
                .Where(j => j.ComUserId == comUserId && j.IsActive)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(j => j.JobStatus == status.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<bool> JobTitleExistsInCompanyAsync(string title, int companyId)
        {
            return await _context.Jobs
                .AnyAsync(j => j.CompanyId == companyId && j.Title == title && j.IsActive);
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


