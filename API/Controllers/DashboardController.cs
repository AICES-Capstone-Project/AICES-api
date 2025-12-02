using API.Common;
using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("top-category-spec")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetTopCategorySpecByResumeCount([FromQuery] int top = 10)
        {
            var serviceResponse = await _dashboardService.GetTopCategorySpecByResumeCountAsync(top);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

