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
        Task<ServiceResponse> CreateJobAsync(JobRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetJobByIdAsync(int jobId);
        Task<ServiceResponse> GetJobsAsync(int page = 1, int pageSize = 10, string? search = null);
    }
}


