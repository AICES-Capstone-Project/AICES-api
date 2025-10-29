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

        [HttpGet("company/self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company/self/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetSelfCompanyJobByIdAsync(id));

        [HttpPost("company/self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompanySelfCreateJob([FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.CompanySelfCreateJobAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        } 
    }
}


