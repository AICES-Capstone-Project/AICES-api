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
        public async Task<IActionResult> GetSelfCompanyPublishedJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyPublishedJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("published/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyPublishedJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetSelfCompanyPublishedJobByIdAsync(id));

        [HttpGet("pending")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetSelfCompanyJobsPending([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyJobsPendingAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("pending/{id}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetSelfCompanyPendingJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetSelfCompanyPendingJobByIdAsync(id));

        [HttpGet("me")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfJobsByMe([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? status = null)
        {
            var serviceResponse = await _jobService.GetSelfJobsByMeWithStatusStringAsync(page, pageSize, search, status);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfCreateJob([FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.CompanySelfCreateJobAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfUpdateJob(int id, [FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.UpdateSelfCompanyJobAsync(id, request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfDeleteJob(int id)
        {
            var serviceResponse = await _jobService.DeleteSelfCompanyJobAsync(id, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> UpdateSelfCompanyJobStatus(int id, [FromBody] UpdateJobStatusRequest request)
        {
            var response = await _jobService.UpdateSelfCompanyJobStatusAsync(id, request.Status, User);
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

