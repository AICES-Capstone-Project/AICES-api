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

        
        [HttpGet("me")]
        [Authorize(Roles = "HR_Recruiter,HR_Manager")]
        public async Task<IActionResult> GetMyFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _feedbackService.GetMyFeedbacksAsync(User, page, pageSize);
            return ControllerResponse.Response(response);
        }

        
        [HttpPost]
        [Authorize(Roles = "HR_Recruiter,HR_Manager")]
        public async Task<IActionResult> CreateFeedback([FromBody] FeedbackRequest request)
        {
            var response = await _feedbackService.CreateAsync(request, User);
            return ControllerResponse.Response(response);
        }

        
        [HttpDelete("{feedbackId}")]
        [Authorize(Roles = "HR_Recruiter,HR_Manager")]
        public async Task<IActionResult> DeleteMyFeedback(int feedbackId)
        {
            var response = await _feedbackService.DeleteMyFeedbackAsync(feedbackId, User);
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

        
        [HttpGet]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetAllFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _feedbackService.GetAllAsync(page, pageSize);
            return ControllerResponse.Response(response);
        }

        
        [HttpGet("{feedbackId}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetFeedbackDetail(int feedbackId)
        {
            var response = await _feedbackService.GetByIdAsync(feedbackId);
            return ControllerResponse.Response(response);
        }

        
        [HttpDelete("{feedbackId}")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> DeleteFeedback(int feedbackId)
        {
            var response = await _feedbackService.DeleteFeedbackByAdminAsync(feedbackId);
            return ControllerResponse.Response(response);
        }
    }
}
