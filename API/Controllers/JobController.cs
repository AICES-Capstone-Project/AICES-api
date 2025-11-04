using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api")]
    [ApiController]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet("jobs")]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("jobs/{id}")]
        public async Task<IActionResult> GetJobById(int id)
        {
            var serviceResponse = await _jobService.GetJobByIdAsync(id);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company/self/jobs/published")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company/self/jobs/pending")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetSelfCompanyJobsPending([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyJobsPendingAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company/self/jobs/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetSelfCompanyJobByIdAsync(id));

        [HttpPost("company/self/jobs")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfCreateJob([FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.CompanySelfCreateJobAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPatch("company/self/jobs/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfUpdateJob(int id, [FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.UpdateSelfCompanyJobAsync(id, request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpDelete("company/self/jobs/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfDeleteJob(int id)
        {
            var serviceResponse = await _jobService.DeleteSelfCompanyJobAsync(id, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPut("company/self/jobs/{id}/status")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> UpdateSelfCompanyJobStatus(int id, [FromBody] UpdateJobStatusRequest request)
        {
            var response = await _jobService.UpdateSelfCompanyJobStatusAsync(id, request.Status, User);
            return ControllerResponse.Response(response);
        }
    }
}


