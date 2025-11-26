using Data.Entities;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobRepository
    {
        Task AddAsync(Job job);
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
        Task<List<Job>> GetAllJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> GetTotalAllJobsByCompanyIdAsync(int companyId, string? search = null);
        Task<List<Job>> GetJobsByComUserIdAsync(int comUserId, int page, int pageSize, string? search = null, JobStatusEnum? status = null);
        Task<int> GetTotalJobsByComUserIdAsync(int comUserId, string? search = null, JobStatusEnum? status = null);
        Task<bool> JobTitleExistsInCompanyAsync(string title, int companyId);
        void UpdateJob(Job job);
        void SoftDeleteJob(Job job);
    }
}


