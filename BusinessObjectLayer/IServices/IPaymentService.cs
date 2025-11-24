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
        Task<ServiceResponse> GetPaymentHistoryAsync(ClaimsPrincipal user, int page, int pageSize);
        Task<ServiceResponse> CancelSubscriptionAsync(ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetCurrentSubscriptionAsync(ClaimsPrincipal userClaims);
    }

}
