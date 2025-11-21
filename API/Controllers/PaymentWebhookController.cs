using BusinessObjectLayer.IServices;
using Data.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/payments/webhook")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentWebhookController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"];

            var result = await _paymentService.HandleWebhookAsync(json, signature);

            if (result.Status == SRStatus.Error)
                return BadRequest(result.Message);

            return Ok(result.Message);
        }
    }
}
