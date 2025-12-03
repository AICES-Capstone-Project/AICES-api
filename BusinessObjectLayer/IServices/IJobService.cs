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
        Task<ServiceResponse> CompanySelfCreateJobAsync(JobRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetJobByIdAsync(int jobId, int companyId);
        Task<ServiceResponse> GetJobsAsync(int companyId, int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetSelfCompanyPublishedJobsAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetSelfCompanyJobsPendingAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetSelfJobsByMeAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.JobStatusEnum? status = null);
        Task<ServiceResponse> GetSelfJobsByMeWithStatusStringAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null);
        Task<ServiceResponse> GetSelfCompanyPublishedJobByIdAsync(int jobId);
        Task<ServiceResponse> GetSelfCompanyPendingJobByIdAsync(int jobId);
        Task<ServiceResponse> UpdateSelfCompanyJobAsync(int jobId, JobRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> DeleteSelfCompanyJobAsync(int jobId, ClaimsPrincipal userClaims);
        Task<ServiceResponse> UpdateSelfCompanyJobStatusAsync(int jobId, Data.Enum.JobStatusEnum status, ClaimsPrincipal userClaims);
    }
}


