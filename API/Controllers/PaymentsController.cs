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

        [HttpGet]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetPayments()
        {
            var result = await _paymentService.GetPaymentsAsync(User);
            return ControllerResponse.Response(result);
        }

        [HttpGet("{paymentId}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetPaymentDetail(int paymentId)
        {
            var result = await _paymentService.GetPaymentDetailAsync(User, paymentId);
            return ControllerResponse.Response(result);
        }

        [HttpGet("stripe/session")]
        public async Task<IActionResult> GetPaymentBySessionId([FromQuery] string sessionId)
        {
            var result = await _paymentService.GetPaymentBySessionIdAsync(sessionId);
            return ControllerResponse.Response(result);
        }

    }


}
