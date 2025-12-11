using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/feedbacks")]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        /// <summary>
        /// Get feedbacks created by the current HR user (HR_Recruiter, HR_Manager only)
        /// </summary>
        [HttpGet("me")]
        [Authorize(Roles = "HR_Recruiter,HR_Manager")]
        public async Task<IActionResult> GetMyFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _feedbackService.GetMyFeedbacksAsync(User, page, pageSize);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Create a new feedback (HR_Recruiter, HR_Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "HR_Recruiter,HR_Manager")]
        public async Task<IActionResult> CreateFeedback([FromBody] FeedbackRequest request)
        {
            var response = await _feedbackService.CreateAsync(request, User);
            return ControllerResponse.Response(response);
        }
    }

    [ApiController]
    [Route("api/system/feedbacks")]
    public class SystemFeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public SystemFeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        /// <summary>
        /// Get all feedbacks with pagination (System_Admin, System_Manager, System_Staff only)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetAllFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _feedbackService.GetAllAsync(page, pageSize);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Get feedback detail by ID (System_Admin, System_Manager, System_Staff only)
        /// </summary>
        [HttpGet("{feedbackId}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetFeedbackDetail(int feedbackId)
        {
            var response = await _feedbackService.GetByIdAsync(feedbackId);
            return ControllerResponse.Response(response);
        }
    }
}
