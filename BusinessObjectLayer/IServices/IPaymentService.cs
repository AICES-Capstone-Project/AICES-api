using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IPaymentService
    {
        Task<ServiceResponse> CreateCheckoutSessionAsync(CheckoutRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> HandleWebhookAsync(string json, string signature);
        Task<ServiceResponse> GetPaymentsAsync(ClaimsPrincipal user);
        Task<ServiceResponse> GetPaymentDetailAsync(ClaimsPrincipal user, int paymentId);
        Task<ServiceResponse> GetPaymentBySessionIdAsync(string sessionId);
        Task<ServiceResponse> CancelSubscriptionAsync(ClaimsPrincipal userClaims);
    }

}
