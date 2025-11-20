using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [Authorize(Roles = "HR_Manager")]
        [HttpPost("checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request)
        {
            var result = await _paymentService.CreateCheckoutSessionAsync(request, User);
            return ControllerResponse.Response(result);
        }
        [HttpGet("history")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetPaymentHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _paymentService.GetPaymentHistoryAsync(User, page, pageSize);
            return ControllerResponse.Response(result);
        }


    }

}
