using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/subscriptions")]
    [ApiController]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet] 
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _subscriptionService.GetAllByAdminAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdForAdmin(int id)
        {
            var response = await _subscriptionService.GetByIdForAdminAsync(id);
            return ControllerResponse.Response(response);
        }



        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SubscriptionRequest request)
        {
            var response = await _subscriptionService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SubscriptionRequest request)
        {
            var response = await _subscriptionService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _subscriptionService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}
