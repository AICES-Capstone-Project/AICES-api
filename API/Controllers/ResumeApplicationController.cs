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
    }
}
