using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("webhook/stripe")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public StripeWebhookController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> Index()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"];

            var result = await _paymentService.HandleWebhookAsync(json, signature);
            if (result.Status == Data.Enum.SRStatus.Success)
                return Ok();

            return BadRequest(result);
        }
    }
}
