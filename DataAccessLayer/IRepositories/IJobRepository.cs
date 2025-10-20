using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobRepository
    {
        Task<Job> CreateJobAsync(Job job);
        Task<Job?> GetJobByIdAsync(int jobId);
        Task<bool> CompanyExistsAsync(int companyId);
        Task<bool> CompanyUserExistsAsync(int comUserId);
        Task<CompanyUser?> GetCompanyUserByUserIdAsync(int userId);
        Task AddJobCategoriesAsync(List<JobCategory> jobCategories);
        Task AddJobEmploymentTypesAsync(List<JobEmploymentType> jobEmploymentTypes);
        Task AddCriteriaAsync(List<Criteria> criteria);
    }
}


