using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/candidates")]
    [ApiController]
    [Authorize]
    public class CandidateController : ControllerBase
    {
        private readonly ICandidateService _candidateService;

        public CandidateController(ICandidateService candidateService)
        {
            _candidateService = candidateService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _candidateService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}/resumes")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _candidateService.GetByIdWithResumesAsync(id);
            return ControllerResponse.Response(response);
        }

        // [HttpPost]
        // [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        // public async Task<IActionResult> Create([FromBody] CandidateCreateRequest request)
        // {
        //     var response = await _candidateService.CreateAsync(request);
        //     return ControllerResponse.Response(response);
        // }

        [HttpPatch("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> Update(int id, [FromBody] CandidateUpdateRequest request)
        {
            var response = await _candidateService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _candidateService.DeleteAsync(id);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// GET /api/candidates/resumes/{resumeId}/applications
        /// Get all applications for a specific resume with pagination and filtering
        /// Query params: page, pageSize, search (job title, company name, campaign title), minScore, maxScore, applicationStatus, sortBy (HighestScore/NewestCreated)
        /// </summary>
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        [HttpGet("resumes/{resumeId}/applications")]
        public async Task<IActionResult> GetResumeApplications(
            int resumeId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] decimal? minScore = null,
            [FromQuery] decimal? maxScore = null,
            [FromQuery] Data.Enum.ApplicationStatusEnum? applicationStatus = null,
            [FromQuery] Data.Enum.ResumeSortByEnum sortBy = Data.Enum.ResumeSortByEnum.HighestScore)
        {
            var request = new GetResumeApplicationsRequest
            {
                Page = page,
                PageSize = pageSize,
                Search = search,
                MinScore = minScore,
                MaxScore = maxScore,
                ApplicationStatus = applicationStatus,
                SortBy = sortBy
            };
            var response = await _candidateService.GetResumeApplicationsAsync(resumeId, request);
            return ControllerResponse.Response(response);
        }

        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        [HttpGet("resumes/{resumeId}/applications/{applicationId}")]
        public async Task<IActionResult> GetResumeApplicationDetail(int resumeId, int applicationId)
        {
            var response = await _candidateService.GetResumeApplicationDetailAsync(resumeId, applicationId);
            return ControllerResponse.Response(response);
        }
    }
}


