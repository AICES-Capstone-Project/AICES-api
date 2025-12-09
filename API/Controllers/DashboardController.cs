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

        [HttpGet("summary")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var serviceResponse = await _dashboardService.GetDashboardSummaryAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("top-rated-candidates")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetTopRatedCandidates([FromQuery] int limit = 5)
        {
            var serviceResponse = await _dashboardService.GetTopRatedCandidatesAsync(limit);
            return ControllerResponse.Response(serviceResponse);
        }
    }

    [Route("api/system/dashboard")]
    [ApiController]
    public class SystemDashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public SystemDashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

       
        [HttpGet("overview")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetSystemOverview()
        {
            var serviceResponse = await _dashboardService.GetSystemOverviewAsync();
            return ControllerResponse.Response(serviceResponse);
        }
       
        [HttpGet("companies")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetSystemCompanyStats()
        {
            var serviceResponse = await _dashboardService.GetSystemCompanyStatsAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("company-subscriptions")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetSystemCompanySubscriptions()
        {
            var serviceResponse = await _dashboardService.GetSystemCompanySubscriptionsAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("top-companies")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetSystemTopCompanies([FromQuery] int top = 10)
        {
            var serviceResponse = await _dashboardService.GetSystemTopCompaniesAsync(top);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

