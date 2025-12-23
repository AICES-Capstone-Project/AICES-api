using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/resume-applications")]
    [ApiController]
    [Authorize(Roles = "HR_Manager,HR_Recruiter")]
    public class ResumeApplicationController : ControllerBase
    {
        private readonly IResumeApplicationService _resumeApplicationService;

        public ResumeApplicationController(IResumeApplicationService resumeApplicationService)
        {
            _resumeApplicationService = resumeApplicationService;
        }
        
        [HttpPatch("{applicationId}/adjusted-score")]
        public async Task<IActionResult> UpdateAdjustedScore(int applicationId, [FromBody] UpdateAdjustedScoreRequest request)
        {
            var response = await _resumeApplicationService.UpdateAdjustedScoreAsync(applicationId, request, User);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{applicationId}/status")]
        public async Task<IActionResult> UpdateStatus(int applicationId, [FromBody] UpdateApplicationStatusRequest request)
        {
            var response = await _resumeApplicationService.UpdateStatusAsync(applicationId, request, User);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// GET /api/campaigns/{campaignId}/jobs/{jobId}/resumes
        /// Get list of resumes for a specific job in a campaign with pagination and filtering
        /// Query params: page, pageSize, search (name, email, phone), minScore, maxScore, applicationStatus
        /// Returns: paginated list of resumes with resumeId, status, fullName, totalResumeScore
        /// </summary>
        [HttpGet("/api/campaigns/{campaignId}/jobs/{jobId}/resumes")]
        public async Task<IActionResult> GetJobResumes(
            int campaignId, 
            int jobId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] decimal? minScore = null,
            [FromQuery] decimal? maxScore = null,
            [FromQuery] Data.Enum.ApplicationStatusEnum? applicationStatus = null)
        {
            var request = new GetJobResumesRequest
            {
                Page = page,
                PageSize = pageSize,
                Search = search,
                MinScore = minScore,
                MaxScore = maxScore,
                ApplicationStatus = applicationStatus
            };
            var serviceResponse = await _resumeApplicationService.GetJobResumesAsync(jobId, campaignId, request);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/campaigns/{campaignId}/jobs/{jobId}/resumes/{applicationId}
        /// Get detailed information about a specific resume application in a campaign
        /// Returns: resume details, candidate info, AI scores, and score details
        /// </summary>
        [HttpGet("/api/campaigns/{campaignId}/jobs/{jobId}/resumes/{applicationId}")]
        public async Task<IActionResult> GetJobResumeDetail(int campaignId, int jobId, int applicationId)
        {
            var serviceResponse = await _resumeApplicationService.GetJobResumeDetailAsync(jobId, applicationId, campaignId);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}
