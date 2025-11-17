using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/resume")]
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
            if (request.File == null || request.File.Length == 0)
            {
                var errorResponse = new Data.Models.Response.ServiceResponse
                {
                    Status = Data.Enum.SRStatus.Validation,
                    Message = "File is required."
                };
                return ControllerResponse.Response(errorResponse);
            }

            var serviceResponse = await _resumeService.UploadResumeAsync(request.JobId, request.File);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// Receive AI processing result callback from Python service
        /// </summary>
         [HttpPost("result")]
        public async Task<IActionResult> ProcessAIResult([FromBody] AIResultRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errorResponse = new Data.Models.Response.ServiceResponse
                {
                    Status = Data.Enum.SRStatus.Validation,
                    Message = "Validation failed.",
                    Data = ModelState
                };
                return ControllerResponse.Response(errorResponse);
            }

            var serviceResponse = await _resumeService.ProcessAIResultAsync(request);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// Get resume processing result by resume ID
        /// </summary>
         [HttpGet("result/{resumeId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetResumeResult(int resumeId)
        {
            var serviceResponse = await _resumeService.GetResumeResultAsync(resumeId);
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// GET /api/resume/jobs/{jobId}
        /// Get list of resumes for a specific job (company self)
        /// Returns: resumeId, status, fullName, totalResumeScore
        /// </summary>
        [HttpGet("jobs/{jobId}")]
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
        [HttpGet("jobs/{jobId}/{resumeId}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetJobResumeDetail(int jobId, int resumeId)
        {
            var serviceResponse = await _resumeService.GetJobResumeDetailAsync(jobId, resumeId);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

