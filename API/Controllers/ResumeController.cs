using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Data.Models.Response;
using Data.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/resumes")]
    [ApiController]
    public class ResumeController : ControllerBase
    {
        private readonly IResumeService _resumeService;
        private readonly IComparisonService _comparisonService;

        public ResumeController(IResumeService resumeService, IComparisonService comparisonService)
        {
            _resumeService = resumeService;
            _comparisonService = comparisonService;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> UploadResume([FromForm] ResumeUploadRequest request) 
        {
            // Optional guard to ensure route jobId matches request payload
            if (request == null)
            {
                return ControllerResponse.Response(new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Request is required."
                });
            }

            if (request.File == null || request.File.Length == 0)
            {
                return ControllerResponse.Response(new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "File is required."
                });
            }

            var serviceResponse = await _resumeService.UploadResumeAsync(request.CampaignId, request.JobId, request.File);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// POST /api/jobs/{jobId}/resumes/{resumeId}/resend
        /// Resend a completed resume for AI re-scoring (advanced analysis)
        /// Uses existing parsed data, does not re-parse the resume
        /// </summary>
        // [HttpPost("/api/jobs/{jobId}/resumes/{resumeId}/resend")]
        // [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        // public async Task<IActionResult> ResendResume(int jobId, int resumeId)
        // {
        //     var serviceResponse = await _resumeService.ResendResumeAsync(jobId, resumeId);
        //     return ControllerResponse.Response(serviceResponse);
        // }

        /// <summary>
        /// Receive AI processing result callback from Python service
        /// </summary>
        [HttpPost("result/ai")]
        public async Task<IActionResult> ProcessAIResult([FromBody] AIResultRequest request)
        {
            var serviceResponse = await _resumeService.ProcessAIResultAsync(request);
            return ControllerResponse.Response(serviceResponse);
        }
        
        /// <summary>
        /// GET /api/campaigns/{campaignId}/jobs/{jobId}/resumes
        /// Get list of resumes for a specific job in a campaign
        /// Returns: resumeId, status, fullName, totalResumeScore
        /// </summary>
        [HttpGet("/api/campaigns/{campaignId}/jobs/{jobId}/resumes")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")] 
        public async Task<IActionResult> GetJobResumes(int campaignId, int jobId)
        {
            var serviceResponse = await _resumeService.GetJobResumesAsync(jobId, campaignId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/campaigns/{campaignId}/jobs/{jobId}/resumes/{applicationId}
        /// Get detailed information about a specific resume application in a campaign
        /// Returns: resume details, candidate info, AI scores, and score details
        /// </summary>
        [HttpGet("/api/campaigns/{campaignId}/jobs/{jobId}/resumes/{applicationId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetJobResumeDetail(int campaignId, int jobId, int applicationId)
        {
            var serviceResponse = await _resumeService.GetJobResumeDetailAsync(jobId, applicationId, campaignId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// POST /api/resume/{resumeId}/retry
        /// Retry a failed resume by re-pushing it to the Redis queue
        /// Flow: Check status = Failed -> Re-push to Redis with new queueJobId -> Update status = Pending
        /// </summary>
        // [HttpPost("{resumeId}/retry")]
        // [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        // public async Task<IActionResult> RetryFailedResume(int resumeId)
        // {
        //     var serviceResponse = await _resumeService.RetryFailedResumeAsync(resumeId);
        //     return ControllerResponse.Response(serviceResponse);
        // }

        /// <summary>
        /// DELETE /api/resume/{applicationId}
        /// Soft delete a resume application by setting IsActive = false
        /// </summary>
        [HttpDelete("{applicationId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> SoftDeleteResume(int applicationId)
        {
            var serviceResponse = await _resumeService.SoftDeleteResumeAsync(applicationId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// POST /api/resumes/compare
        /// Compare 2-5 candidates for a job/campaign
        /// Request: { jobId, campaignId, applicationIds: [101, 102, 103] }
        /// Returns: { comparisonId, queueJobId, applicationIds }
        /// </summary>
        [HttpPost("compare")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> CompareApplications([FromBody] CompareApplicationsRequest request)
        {
            var serviceResponse = await _comparisonService.CompareApplicationsAsync(request);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// POST /api/resumes/result/ai/comparison
        /// Receive AI comparison result callback from Python service
        /// </summary>
        [HttpPost("result/ai/comparison")]
        public async Task<IActionResult> ProcessComparisonAIResult([FromBody] ComparisonAIResultRequest request)
        {
            var serviceResponse = await _comparisonService.ProcessComparisonAIResultAsync(request);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/resumes/comparisons/{comparisonId}
        /// Get comparison result by ID
        /// Returns: comparison details including status, result JSON, and metadata
        /// </summary>
        [HttpGet("comparisons/{comparisonId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetComparisonResult(int comparisonId)
        {
            var serviceResponse = await _comparisonService.GetComparisonResultAsync(comparisonId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/resumes/comparisons/job/{jobId}/campaign/{campaignId}
        /// Get all comparisons for a specific job and campaign
        /// Returns: list of comparisons with their status and metadata
        /// </summary>
        [HttpGet("comparisons/job/{jobId}/campaign/{campaignId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetComparisonsByJobAndCampaign(int jobId, int campaignId)
        {
            var serviceResponse = await _comparisonService.GetComparisonsByJobAndCampaignAsync(jobId, campaignId);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

