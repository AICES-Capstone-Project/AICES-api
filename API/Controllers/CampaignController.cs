using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/campaigns")]
    [ApiController]
    [Authorize]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;

        public CampaignController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] CampaignStatusEnum? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var response = await _campaignService.GetAllAsync(page, pageSize, search, status, startDate, endDate);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _campaignService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}/jobs")]
        public async Task<IActionResult> GetCampaignJobs(int id)
        {
            var response = await _campaignService.GetCampaignJobsAsync(id);
            return ControllerResponse.Response(response);
        }

        [HttpPost]
        [Authorize(Roles = "HR_Manager,HR_Recruiter")]
        public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
        {
            var response = await _campaignService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "HR_Manager,HR_Recruiter")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCampaignRequest request)
        {
            var response = await _campaignService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpPost("{id}/jobs")]
        [Authorize(Roles = "HR_Manager,HR_Recruiter")]
        public async Task<IActionResult> AddJobsToCampaign(int id, [FromBody] AddJobsToCampaignRequest request)
        {
            var response = await _campaignService.AddJobsToCampaignAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}/jobs")]
        [Authorize(Roles = "HR_Manager,HR_Recruiter")]
        public async Task<IActionResult> RemoveJobsFromCampaign(int id, [FromBody] RemoveJobsFromCampaignRequest request)
        {
            var response = await _campaignService.RemoveJobsFromCampaignAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "HR_Manager,HR_Recruiter")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _campaignService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }

        // [HttpGet("me")]
        // public async Task<IActionResult> GetMyCampaigns()
        // {
        //     var response = await _campaignService.GetMyCampaignsAsync();
        //     return ControllerResponse.Response(response);
        // }
    }
}

