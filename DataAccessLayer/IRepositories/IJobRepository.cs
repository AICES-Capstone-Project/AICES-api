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
        Task<List<Job>> GetPublishedJobsAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalPublishedJobsAsync(string? search = null);
        Task<List<Job>> GetPublishedJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> GetTotalPublishedJobsByCompanyIdAsync(int companyId, string? search = null);
        Task<List<Job>> GetPendingJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> GetTotalPendingJobsByCompanyIdAsync(int companyId, string? search = null);
        Task<Job?> GetPublishedJobByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<Job?> GetPendingJobByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<Job?> GetAllJobByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<bool> JobTitleExistsInCompanyAsync(string title, int companyId);
        Task UpdateJobAsync(Job job);
        Task SoftDeleteJobAsync(Job job);
    }
}


