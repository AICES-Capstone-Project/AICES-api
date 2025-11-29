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
        private readonly IPaymentService _paymentService;
        private readonly ICompanySubscriptionService _companySubscriptionService;

        public CompanySubscriptionController(IPaymentService paymentService, ICompanySubscriptionService companySubscriptionService)
        {
            _paymentService = paymentService;
            _companySubscriptionService = companySubscriptionService;
        }

        [HttpPost("cancel")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> CancelSubscription()
        {
            var result = await _paymentService.CancelSubscriptionAsync(User);
            return ControllerResponse.Response(result);
        }

        [HttpGet("current-subscription")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetCurrentSubscription()
        {
            var result = await _companySubscriptionService.GetCurrentSubscriptionAsync(User);
            return ControllerResponse.Response(result);
        }
    }
    
    [Route("api/system/company-subscriptions")]
    [ApiController]
    public class SystemCompanySubscriptionController : ControllerBase
    {
        private readonly ICompanySubscriptionService _companySubscriptionService;

        public SystemCompanySubscriptionController(ICompanySubscriptionService companySubscriptionService)
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

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _companySubscriptionService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}

