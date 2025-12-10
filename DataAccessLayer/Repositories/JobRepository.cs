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

        public async Task AddAsync(Job job)
        {
            await _context.Jobs.AddAsync(job);
        }

        public async Task<Job?> GetJobByIdAsync(int jobId)
        {
            return await _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .FirstOrDefaultAsync(j => j.IsActive && j.JobId == jobId);
        }

        public async Task<Job?> GetForUpdateAsync(int jobId)
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .FirstOrDefaultAsync(j => j.IsActive && j.JobId == jobId);
        }

        public async Task<Job?> GetForUpdateByIdAndCompanyIdAsync(int jobId, int companyId)
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
                .Where(j => j.IsActive && j.CompanyId == companyId)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<Job?> GetPublishedForUpdateByIdAndCompanyIdAsync(int jobId, int companyId)
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
                .FirstOrDefaultAsync(j => j.IsActive && j.JobId == jobId);
        }

        public async Task<List<Job>> GetPublishedJobsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.IsActive && j.JobStatus == JobStatusEnum.Published)
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
            var query = _context.Jobs
                .AsNoTracking()
                .Where(j => j.IsActive && j.JobStatus == JobStatusEnum.Published)
                .AsQueryable();

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
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.IsActive && j.CompanyId == companyId && j.JobStatus == JobStatusEnum.Published)
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
                .AsNoTracking()
                .Where(j => j.IsActive && j.CompanyId == companyId && j.JobStatus == JobStatusEnum.Published)
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
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.IsActive && j.CompanyId == companyId && j.JobStatus == JobStatusEnum.Pending)
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
                .AsNoTracking()
                .Where(j => j.IsActive && j.CompanyId == companyId && j.JobStatus == JobStatusEnum.Pending)
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
                .AsNoTracking()
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
                .FirstOrDefaultAsync(j => j.IsActive && j.JobId == jobId);
        }

         public async Task<Job?> GetPendingJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive && j.JobStatus == JobStatusEnum.Pending)
                .FirstOrDefaultAsync(j => j.IsActive && j.JobId == jobId);
        }

        public async Task<Job?> GetAllJobByIdAndCompanyIdAsync(int jobId, int companyId)
        {
            return await _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }

        public async Task<List<Job>> GetAllJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null)
        {
            var query = _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
                .Include(j => j.Criteria)
                .Where(j => j.CompanyId == companyId && j.IsActive)
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

        public async Task<int> GetTotalAllJobsByCompanyIdAsync(int companyId, string? search = null)
        {
            var query = _context.Jobs
                .AsNoTracking()
                .Where(j => j.CompanyId == companyId && j.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j => j.Title.Contains(search) || 
                                       (j.Description != null && j.Description.Contains(search)) ||
                                       (j.Requirements != null && j.Requirements.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Job>> GetJobsByComUserIdAsync(int comUserId, int page, int pageSize, string? search = null, JobStatusEnum? status = null)
        {
            var query = _context.Jobs
                .AsNoTracking()
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
                .Include(j => j.Level)
                .Include(j => j.JobLanguages!)
                    .ThenInclude(jl => jl.Language)
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
                .AsNoTracking()
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
                .AsNoTracking()
                .AnyAsync(j => j.CompanyId == companyId && j.Title == title && j.IsActive);
        }

        public async Task<List<string>> GetEmploymentTypesByJobIdAsync(int jobId)
        {
            return await _context.JobEmploymentTypes
                .AsNoTracking()
                .Where(jet => jet.JobId == jobId)
                .Include(jet => jet.EmploymentType)
                .Select(jet => jet.EmploymentType.Name)
                .ToListAsync();
        }

        public async Task<List<string>> GetSkillsByJobIdAsync(int jobId)
        {
            return await _context.JobSkills
                .AsNoTracking()
                .Where(js => js.JobId == jobId)
                .Include(js => js.Skill)
                .Select(js => js.Skill.Name)
                .ToListAsync();
        }

        public void UpdateJob(Job job)
        {
            _context.Jobs.Update(job);
        }

        public void SoftDeleteJob(Job job)
        {
            job.IsActive = false;
            _context.Jobs.Update(job);
        }
    }
}


