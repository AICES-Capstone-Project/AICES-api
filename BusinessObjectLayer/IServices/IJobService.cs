using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IJobService
    {
        Task<ServiceResponse> CreateAsync(JobRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetJobByIdAsync(int jobId, int companyId);
        Task<ServiceResponse> GetJobsAsync(int companyId, int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetCurrentCompanyPublishedListAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetCurrentCompanyPendingListAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetCurrentUserJobsAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.JobStatusEnum? status = null);
        Task<ServiceResponse> GetCurrentUserJobsWithStatusStringAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null);
        Task<ServiceResponse> GetCurrentCompanyPublishedByIdAsync(int jobId);
        Task<ServiceResponse> GetCurrentCompanyPendingByIdAsync(int jobId);
        Task<ServiceResponse> UpdateAsync(int jobId, JobRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> DeleteAsync(int jobId, ClaimsPrincipal userClaims);
        Task<ServiceResponse> UpdateStatusAsync(int jobId, Data.Enum.JobStatusEnum status, ClaimsPrincipal userClaims);
        
    }
}


