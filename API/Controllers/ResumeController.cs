using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/resumes")]
    [ApiController]
    public class ResumeController : ControllerBase
    {
        private readonly IResumeService _resumeService;

        public ResumeController(IResumeService resumeService)
        {
            _resumeService = resumeService;
        }

        /// <summary>
        /// Upload a resume file (PDF/DOCX) for a job
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> UploadResume([FromForm] ResumeUploadRequest request) 
        {
            var serviceResponse = await _resumeService.UploadResumeAsync(request.JobId, request.File);
            return ControllerResponse.Response(serviceResponse);
        }

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
        /// GET /api/resume/jobs/{jobId}
        /// Get list of resumes for a specific job (company self)
        /// Returns: resumeId, status, fullName, totalResumeScore
        /// </summary>
        [HttpGet("/api/jobs/{jobId}/resumes")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")] 
        public async Task<IActionResult> GetJobResumes(int jobId)
        {
            var serviceResponse = await _resumeService.GetJobResumesAsync(jobId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/resume/jobs/{jobId}/{resumeId}
        /// Get detailed information about a specific resume
        /// Returns: resume details, candidate info, AI scores, and score details
        /// </summary>
        [HttpGet("/api/jobs/{jobId}/resumes/{resumeId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetJobResumeDetail(int jobId, int resumeId)
        {
            var serviceResponse = await _resumeService.GetJobResumeDetailAsync(jobId, resumeId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// POST /api/resume/{resumeId}/retry
        /// Retry a failed resume by re-pushing it to the Redis queue
        /// Flow: Check status = Failed -> Re-push to Redis with new queueJobId -> Update status = Pending
        /// </summary>
        [HttpPost("{resumeId}/retry")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> RetryFailedResume(int resumeId)
        {
            var serviceResponse = await _resumeService.RetryFailedResumeAsync(resumeId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// DELETE /api/resume/{resumeId}
        /// Soft delete a resume by setting IsActive = false
        /// </summary>
        [HttpDelete("{resumeId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> SoftDeleteResume(int resumeId)
        {
            var serviceResponse = await _resumeService.SoftDeleteResumeAsync(resumeId);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

