using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Data.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/jobs")]
    [ApiController]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet("published")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetCompanyPublishedJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetCurrentCompanyPublishedListAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("published/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetCompanyPublishedJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetCurrentCompanyPublishedByIdAsync(id));

        [HttpGet("pending")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetCompanyJobsPending([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetCurrentCompanyPendingListAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("pending/{id}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetCompanyPendingJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetCurrentCompanyPendingByIdAsync(id));

        [HttpGet("me")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetJobsByMe([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? status = null)
        {
            var serviceResponse = await _jobService.GetCurrentUserJobsWithStatusStringAsync(page, pageSize, search, status);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanyCreateJob([FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.CreateAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanyUpdateJob(int id, [FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.UpdateAsync(id, request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanyDeleteJob(int id)
        {
            var serviceResponse = await _jobService.DeleteAsync(id, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> UpdateCompanyJobStatus(int id, [FromBody] UpdateJobStatusRequest request)
        {
            var response = await _jobService.UpdateStatusAsync(id, request.Status, User);
            return ControllerResponse.Response(response);
        }
    }
    [Route("api/system")]
    [ApiController]
    public class SystemJobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public SystemJobController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet("company/{companyId}/jobs")]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetJobs(int companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetJobsAsync(companyId, page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company/{companyId}/jobs/{id}")]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetJobById(int companyId, int id)
        {
            var serviceResponse = await _jobService.GetJobByIdAsync(id, companyId);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

