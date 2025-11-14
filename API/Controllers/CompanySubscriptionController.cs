using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/company-subscriptions")]
    [ApiController]
    public class CompanySubscriptionController : ControllerBase
    {
        private readonly ICompanySubscriptionService _companySubscriptionService;

        public CompanySubscriptionController(ICompanySubscriptionService companySubscriptionService)
        {
            _companySubscriptionService = companySubscriptionService;
        }

        [HttpGet]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _companySubscriptionService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _companySubscriptionService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] CreateCompanySubscriptionRequest request)
        {
            var response = await _companySubscriptionService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        // [HttpPatch("{id}")]
        // [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        // public async Task<IActionResult> Update(int id, [FromBody] CompanySubscriptionRequest request)
        // {
        //     var response = await _companySubscriptionService.UpdateAsync(id, request);
        //     return ControllerResponse.Response(response);
        // }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _companySubscriptionService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}

