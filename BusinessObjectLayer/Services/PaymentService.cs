using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Settings;
using Data.Settings.Data.Settings;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly ICompanySubscriptionRepository _companySubRepo;
        private readonly ITransactionRepository _transactionRepo;
        private readonly ICompanyUserRepository _companyUserRepo;
        private readonly StripeSettings _settings;
        private readonly IAuthRepository _authRepo;
        private readonly INotificationService _notificationService;

        public PaymentService(
            IPaymentRepository paymentRepo,
            ISubscriptionRepository subscriptionRepo,
            ICompanySubscriptionRepository companySubRepo,
            ITransactionRepository transactionRepo,
            ICompanyUserRepository companyUserRepo,
            IAuthRepository authRepo,
            INotificationService notificationService,
            IOptions<StripeSettings> settings)
        {
            _paymentRepo = paymentRepo;
            _subscriptionRepo = subscriptionRepo;
            _companySubRepo = companySubRepo;
            _transactionRepo = transactionRepo;
            _companyUserRepo = companyUserRepo;
            _authRepo = authRepo;
            _notificationService = notificationService;
            _settings = settings.Value;
        }

        // ===================================================
        // 1. CREATE CHECKOUT SESSION 
        // ===================================================
        public async Task<ServiceResponse> CreateCheckoutSessionAsync(CheckoutRequest request, ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            var companyUser = await _companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "You must join a company before purchasing a subscription." };

            int companyId = companyUser.CompanyId.Value;

            var subscription = await _subscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Subscription not found" };

            // Check existing active subscription
            var activeSub = await _companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            if (activeSub != null)
                return new ServiceResponse { Status = SRStatus.Validation, Message = "Your company already has an active subscription." };

            // Create a new pending payment
            var payment = new Payment
            {
                CompanyId = companyId,
                PaymentStatus = PaymentStatusEnum.Pending,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await _paymentRepo.AddAsync(payment);

            // Convert VND to USD
            decimal usdPrice = Math.Round(subscription.Price * _settings.VndToUsdRate, 2);
            long stripeAmount = (long)(usdPrice * 100);

            string domain = Environment.GetEnvironmentVariable("APPURL__CLIENTURL") ?? "http://localhost:5173";

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                PaymentMethodTypes = new List<string> { "card" },
                SuccessUrl = $"{domain}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/payment/cancel",
                Metadata = new Dictionary<string, string>
                {
                    { "paymentId", payment.PaymentId.ToString() },
                    { "companyId", companyId.ToString() },
                    { "subscriptionId", request.SubscriptionId.ToString() }
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = stripeAmount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = subscription.Name,
                                Description = subscription.Description
                            }
                        }
                    }
                }
            };

            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(options);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Checkout session created",
                Data = new { url = session.Url }
            };
        }

        // ===================================================
        // 2. HANDLE STRIPE WEBHOOK
        // ===================================================
        public async Task<ServiceResponse> HandleWebhookAsync(string json, string signature)
        {
            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    _settings.WebhookSecret
                );
            }
            catch
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Invalid Stripe signature"
                };
            }

            // ĐÚNG TÊN EVENT CHECKOUT SESSION COMPLETED
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;

                if (session == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Invalid session object"
                    };
                }

                // ====== CHECK METADATA TỪ SESSION THẬT ====
                // Stripe CLI trigger không có metadata → tránh null exception
                if (session.Metadata == null ||
                    !session.Metadata.ContainsKey("paymentId") ||
                    !session.Metadata.ContainsKey("companyId") ||
                    !session.Metadata.ContainsKey("subscriptionId"))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Test event received (missing metadata), ignored."
                    };
                }

                // ===== PARSE SAFE =====
                if (!int.TryParse(session.Metadata["paymentId"], out int paymentId) ||
                    !int.TryParse(session.Metadata["companyId"], out int companyId) ||
                    !int.TryParse(session.Metadata["subscriptionId"], out int subscriptionId))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Invalid metadata format."
                    };
                }

                // ====== BEGIN BUSINESS LOGIC ======

                var payment = await _paymentRepo.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Payment record not found."
                    };
                }

                // Update payment to paid
                payment.PaymentStatus = PaymentStatusEnum.Paid;
                payment.InvoiceUrl = session.Url;
                await _paymentRepo.UpdateAsync(payment);

                // Save transaction
                await _transactionRepo.AddAsync(new Transaction
                {
                    PaymentId = paymentId,
                    Amount = (session.AmountTotal ?? 0) / 100m,
                    Gateway = TransactionGatewayEnum.StripePayment,
                    ResponseCode = "SUCCESS",
                    ResponseMessage = "Stripe checkout completed",
                    TransactionTime = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                // Active subscription
                var subscription = await _subscriptionRepo.GetByIdAsync(subscriptionId);
                var now = DateTime.UtcNow;

                await _companySubRepo.AddAsync(new CompanySubscription
                {
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    StartDate = now,
                    EndDate = now.AddDays(subscription.DurationDays),
                    SubscriptionStatus = SubscriptionStatusEnum.Active,
                    CreatedAt = now,
                    IsActive = true
                });

                // ===== SEND NOTIFICATION TO ADMINS =====
                var admins = await _authRepo.GetUsersByRoleAsync("System_Admin");

                foreach (var admin in admins)
                {
                    await _notificationService.CreateAsync(
                        admin.UserId,
                        NotificationTypeEnum.Subscription,
                        $"A company subscribed to a package",
                        $"Company ID {companyId} has successfully subscribed to '{subscription.Name}'."
                    );
                }


                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription activated successfully."
                };
            }

            // Nếu event không phải checkout.session.completed → bỏ qua
            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Event ignored."
            };
        }

        public async Task<ServiceResponse> GetPaymentHistoryAsync(ClaimsPrincipal userClaims, int page, int pageSize)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            int userId = int.Parse(userIdClaim);

            var companyUser = await _companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "User is not associated with any company." };

            int companyId = companyUser.CompanyId.Value;

            var payments = await _paymentRepo.GetPaymentHistoryByCompanyAsync(companyId, page, pageSize);
            var total = await _paymentRepo.GetTotalPaymentsByCompanyAsync(companyId);

            var responseList = payments.Select(p => new PaymentHistoryResponse
            {
                PaymentId = p.PaymentId,
                Status = p.PaymentStatus,
                CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                TotalAmount = p.Transactions?.Sum(t => t.Amount) ?? 0,

                SubscriptionName = p.Company?.CompanySubscriptions?
                    .OrderByDescending(cs => cs.CreatedAt)
                    .FirstOrDefault()?.Subscription?.Name ?? "",

                DurationDays = p.Company?.CompanySubscriptions?
                    .OrderByDescending(cs => cs.CreatedAt)
                    .FirstOrDefault()?.Subscription?.DurationDays ?? 0,

                Transactions = p.Transactions?.Select(t => new TransactionItem
                {
                    TransactionId = t.TransactionId,
                    Amount = t.Amount,
                    Gateway = t.Gateway,
                    ResponseCode = t.ResponseCode,
                    ResponseMessage = t.ResponseMessage,
                    TransactionTime = t.TransactionTime
                }).ToList() ?? new List<TransactionItem>()
            })
            .ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Payment history retrieved successfully.",
                Data = new
                {
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    Payments = responseList
                }
            };
        }


    }
}
