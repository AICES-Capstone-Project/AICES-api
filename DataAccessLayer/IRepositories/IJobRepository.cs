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
        Task<Job?> GetByIdAsync(int jobId);
        Task<Job?> GetByIdForUpdateAsync(int jobId);
        Task<Job?> GetByIdAndCompanyIdForUpdateAsync(int jobId, int companyId);
        Task<Job?> GetPublishedByIdAndCompanyIdForUpdateAsync(int jobId, int companyId);
        Task<List<Job>> GetPublishedJobsAsync(int page, int pageSize, string? search = null);
        Task<int> CountPublishedAsync(string? search = null);
        Task<List<Job>> GetPublishedJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> CountPublishedByCompanyIdAsync(int companyId, string? search = null);
        Task<List<Job>> GetPendingJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> CountPendingByCompanyIdAsync(int companyId, string? search = null);
        Task<Job?> GetPublishedJobByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<Job?> GetPendingJobByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<Job?> GetByIdAndCompanyIdAsync(int jobId, int companyId);
        Task<List<Job>> GetAllJobsByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null);
        Task<int> CountByCompanyIdAsync(int companyId, string? search = null);
        Task<List<Job>> GetListByCreatorIdAsync(int comUserId, int page, int pageSize, string? search = null, JobStatusEnum? status = null);
        Task<int> CountByCreatorIdAsync(int comUserId, string? search = null, JobStatusEnum? status = null);
        Task<bool> ExistsByTitleAndCompanyIdAsync(string title, int companyId);
        Task UpdateAsync(Job job);
        Task SoftDeleteAsync(Job job);
    }
}


