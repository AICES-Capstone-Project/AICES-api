using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
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

        [HttpGet]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _jobService.GetSelfCompanyJobsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("self/{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyJobById(int id) =>
            ControllerResponse.Response(await _jobService.GetSelfCompanyJobByIdAsync(id));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetJobById(int id)
        {
            var serviceResponse = await _jobService.GetJobByIdAsync(id);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost("self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CreateJob([FromBody] JobRequest request)
        {
            var serviceResponse = await _jobService.CreateJobAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        } 
    }
}


