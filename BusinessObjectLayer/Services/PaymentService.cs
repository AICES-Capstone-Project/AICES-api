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
        private readonly ICompanyRepository _companyRepo;
        private readonly INotificationService _notificationService;

        public PaymentService(
            IPaymentRepository paymentRepo,
            ISubscriptionRepository subscriptionRepo,
            ICompanySubscriptionRepository companySubRepo,
            ITransactionRepository transactionRepo,
            ICompanyUserRepository companyUserRepo,
            IAuthRepository authRepo,
            INotificationService notificationService,
            ICompanyRepository companyRepo,
            IOptions<StripeSettings> settings)
        {
            _paymentRepo = paymentRepo;
            _subscriptionRepo = subscriptionRepo;
            _companySubRepo = companySubRepo;
            _transactionRepo = transactionRepo;
            _companyUserRepo = companyUserRepo;
            _authRepo = authRepo;
            _notificationService = notificationService;
            _companyRepo = companyRepo;
            _settings = settings.Value;
        }

        // Helper method để lấy giờ Việt Nam (UTC+7) nhưng trả về UTC để lưu vào database
        // PostgreSQL yêu cầu DateTime.Kind = UTC cho timestamp with time zone
        // Giờ Việt Nam sẽ được lưu dưới dạng UTC, khi query ra sẽ convert sang VN time nếu cần
        private static DateTime GetVietnamTime()
        {
            // Lưu UTC vào database (PostgreSQL sẽ tự động handle timezone)
            // Khi cần hiển thị, convert sang giờ Việt Nam ở frontend hoặc khi query
            return DateTime.UtcNow;
        }

        // ===================================================
        // 1. CREATE CHECKOUT SESSION 
        // ===================================================
        // using Stripe.Checkout; using Stripe; (ở đầu file)

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

            var existingCompanySubscription = await _companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            if (existingCompanySubscription != null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Your company already has an active subscription. Please cancel or wait for it to expire before purchasing another plan."
                };
            }

            // Lấy priceId: ưu tiên lưu trong Subscription.StripePriceId, nếu null/empty -> dùng env STRIPE__PRICE_DEFAULT
            string stripePriceId = !string.IsNullOrWhiteSpace(subscription.StripePriceId) 
                ? subscription.StripePriceId
                : Environment.GetEnvironmentVariable("STRIPE__PRICE_DEFAULT")
                  ?? throw new Exception($"Stripe price id not configured for subscription '{subscription.Name}' (ID: {subscription.SubscriptionId}). Please set StripePriceId in database or configure STRIPE__PRICE_DEFAULT in environment variables.");

            // Ensure company has Stripe customer
            var company = await _companyRepo.GetByIdAsync(companyId);
            if (company == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company not found" };

            var customerId = company.StripeCustomerId;
            var customerService = new CustomerService();

            if (string.IsNullOrEmpty(customerId))
            {
                // Try to set an email from user profile (falls back to null)
                var companyUserEntity = await _companyUserRepo.GetCompanyUserByUserIdAsync(userId);
                string email = companyUserEntity?.User?.Email ?? null;

                var cust = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = email,
                    Metadata = new Dictionary<string, string> { { "companyId", companyId.ToString() } }
                });
                company.StripeCustomerId = cust.Id;
                await _companyRepo.UpdateAsync(company);
                customerId = cust.Id;
            }

            string domain = Environment.GetEnvironmentVariable("APPURL__CLIENTURL") ?? "http://localhost:5173";

            // Create a pending payment record before redirecting user to Stripe
            var payment = new Payment
            {
                CompanyId = companyId,
                PaymentStatus = PaymentStatusEnum.Pending,
                CreatedAt = GetVietnamTime(),
                IsActive = true
            };
            await _paymentRepo.AddAsync(payment);

            var metadata = new Dictionary<string, string>
            {
                { "companyId", companyId.ToString() },
                { "subscriptionId", subscription.SubscriptionId.ToString() },
                { "paymentId", payment.PaymentId.ToString() }
            };

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                SuccessUrl = $"{domain}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/payment/cancel",
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>(metadata)
                },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Price = stripePriceId,
                Quantity = 1
            }
        }
            };

            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(options);

            // We can add paymentId into session metadata? Stripe session metadata is already set; we could store mapping in our DB if needed.

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
                stripeEvent = EventUtility.ConstructEvent(json, signature, _settings.WebhookSecret);
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Invalid Stripe signature: {ex.Message}"
                };
            }

            // ============================================================
            // 1) checkout.session.completed
            // ============================================================
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Session object missing." };

                int.TryParse(session.Metadata?.GetValueOrDefault("companyId") ?? "0", out int companyId);
                int.TryParse(session.Metadata?.GetValueOrDefault("subscriptionId") ?? "0", out int subscriptionId);

                // Lấy subscription ID từ session
                // Trong Stripe.NET, session.Subscription có thể là string ID hoặc Subscription object
                string stripeSubscriptionId = null;
                
                if (session.Subscription != null)
                {
                    // Kiểm tra type và lấy ID
                    if (session.Subscription is Stripe.Subscription subObject)
                    {
                        stripeSubscriptionId = subObject.Id;
                    }
                    else
                    {
                        // Nếu là string, convert sang string
                        stripeSubscriptionId = session.Subscription.ToString();
                    }
                }

                // Nếu vẫn không có, thử retrieve session với expand
                if (string.IsNullOrEmpty(stripeSubscriptionId))
                {
                    try
                    {
                        var sessionService = new SessionService();
                        var expandedSession = await sessionService.GetAsync(session.Id, new SessionGetOptions
                        {
                            Expand = new List<string> { "subscription" }
                        });
                        
                        if (expandedSession.Subscription != null)
                        {
                            if (expandedSession.Subscription is Stripe.Subscription sub)
                            {
                                stripeSubscriptionId = sub.Id;
                            }
                            else
                            {
                                stripeSubscriptionId = expandedSession.Subscription.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error nhưng không throw
                        Console.WriteLine($"Error retrieving session with expand: {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(stripeSubscriptionId))
                {
                    // Trả về Error để Stripe biết có vấn đề và có thể retry
                    // Hoặc log để debug vì đây là trường hợp không bình thường
                    Console.WriteLine($"[Webhook Error] checkout.session.completed - Session {session.Id} has no subscription ID. Session mode: {session.Mode}");
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Session has no subscription id. Cannot process payment." };
                }

                var existing = await _companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId);
                if (existing != null)
                {
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription already processed." };
                }

                var subscriptionEntity = await _subscriptionRepo.GetByIdAsync(subscriptionId);
                var now = GetVietnamTime();

                var companySubscription = new CompanySubscription
                {
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    StartDate = now,
                    EndDate = now.AddDays(subscriptionEntity?.DurationDays ?? 30),
                    SubscriptionStatus = SubscriptionStatusEnum.Active,
                    StripeSubscriptionId = stripeSubscriptionId,
                    CreatedAt = now,
                    IsActive = true
                };

                await _companySubRepo.AddAsync(companySubscription);

                var admins = await _authRepo.GetUsersByRoleAsync("System_Admin");
                foreach (var admin in admins)
                {
                    await _notificationService.CreateAsync(
                        admin.UserId,
                        NotificationTypeEnum.Subscription,
                        "A company subscribed",
                        $"Company {companyId} subscribed to {subscriptionEntity?.Name} (stripeSub: {stripeSubscriptionId})"
                    );
                }

                return new ServiceResponse { Status = SRStatus.Success, Message = "checkout.session.completed handled." };
            }

            // ============================================================
            // 2) invoice.payment_succeeded
            // ============================================================
            if (stripeEvent.Type == "invoice.payment_succeeded")
            {
                var invoice = stripeEvent.Data.Object as Stripe.Invoice;
                if (invoice == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Invoice missing." };

                // Dùng reflection để tương thích với nhiều phiên bản Stripe.Net khác nhau.
                var stripeSubId = GetInvoiceSubscriptionId(invoice);
                if (string.IsNullOrEmpty(stripeSubId))
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Invoice missing subscription id." };

                long amountPaidCents = invoice.AmountPaid;
                decimal amountPaid = amountPaidCents / 100m;

                var companySub = await _companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubId);

                // Lấy metadata để xử lý payment ngay cả khi companySub chưa tồn tại
                var metadataSnapshot = ExtractInvoiceMetadata(invoice);
                int? companyIdFromMetadata = null;
                if (metadataSnapshot != null && metadataSnapshot.TryGetValue("companyId", out var companyIdStr))
                {
                    if (int.TryParse(companyIdStr, out var companyId))
                    {
                        companyIdFromMetadata = companyId;
                    }
                }

                var companyIdToUse = companySub?.CompanyId ?? companyIdFromMetadata;

                if (companyIdToUse.HasValue)
                {
                    var payment = await GetPaymentFromInvoiceMetadataAsync(metadataSnapshot, companyIdToUse);

                    var invoiceUrl = await GetInvoiceUrlAsync(invoice);

                    if (payment != null)
                    {
                        payment.PaymentStatus = PaymentStatusEnum.Paid;
                        payment.InvoiceUrl = invoiceUrl;
                        await _paymentRepo.UpdateAsync(payment);
                    }
                    else
                    {
                        payment = new Payment
                        {
                            CompanyId = companyIdToUse.Value,
                            PaymentStatus = PaymentStatusEnum.Paid,
                            CreatedAt = GetVietnamTime(),
                            IsActive = true,
                            InvoiceUrl = invoiceUrl
                        };
                        await _paymentRepo.AddAsync(payment);
                    }

                    await _transactionRepo.AddAsync(new Transaction
                    {
                        PaymentId = payment.PaymentId,
                        Amount = amountPaid,
                        Gateway = TransactionGatewayEnum.StripePayment,
                        ResponseCode = "SUCCESS",
                        ResponseMessage = $"Invoice {invoice.Id} paid",
                        TransactionTime = GetVietnamTime(),
                        CreatedAt = GetVietnamTime(),
                        IsActive = true
                    });
                }

                // Update companySub nếu tồn tại (cho renewal)
                if (companySub != null)
                {
                    var subDef = await _subscriptionRepo.GetByIdAsync(companySub.SubscriptionId);
                    var now = GetVietnamTime();
                    var extendFrom = companySub.EndDate > now ? companySub.EndDate : now;

                    companySub.EndDate = extendFrom.AddDays(subDef?.DurationDays ?? 30);
                    companySub.SubscriptionStatus = SubscriptionStatusEnum.Active;
                    await _companySubRepo.UpdateAsync(companySub);
                }

                return new ServiceResponse { Status = SRStatus.Success, Message = "invoice.payment_succeeded handled." };
            }

            // ============================================================
            // 3) invoice.payment_failed
            // ============================================================
            if (stripeEvent.Type == "invoice.payment_failed")
            {
                var invoice = stripeEvent.Data.Object as Stripe.Invoice;
                if (invoice != null)
                {
                    var stripeSubId = GetInvoiceSubscriptionId(invoice);
                    if (string.IsNullOrEmpty(stripeSubId))
                    {
                        return new ServiceResponse { Status = SRStatus.Error, Message = "Invoice missing subscription id." };
                    }

                    var companySub = await _companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubId);
                    if (companySub != null)
                    {
                        companySub.SubscriptionStatus = SubscriptionStatusEnum.Pending;
                        await _companySubRepo.UpdateAsync(companySub);
                    }

                    var metadataSnapshot = ExtractInvoiceMetadata(invoice);
                    var payment = await GetPaymentFromInvoiceMetadataAsync(metadataSnapshot, companySub?.CompanyId);
                    if (payment != null)
                    {
                        payment.PaymentStatus = PaymentStatusEnum.Failed;
                        await _paymentRepo.UpdateAsync(payment);
                    }
                }

                return new ServiceResponse { Status = SRStatus.Success, Message = "invoice.payment_failed handled." };
            }

            // ============================================================
            // 4) customer.subscription.deleted
            // ============================================================
            if (stripeEvent.Type == "customer.subscription.deleted")
            {
                var stripeSub = stripeEvent.Data.Object as Stripe.Subscription;
                if (stripeSub != null)
                {
                    string stripeId = stripeSub.Id;

                    var cs = await _companySubRepo.GetByStripeSubscriptionIdAsync(stripeId);
                    if (cs != null)
                    {
                        cs.SubscriptionStatus = SubscriptionStatusEnum.Canceled;
                        cs.IsActive = false;
                        await _companySubRepo.UpdateAsync(cs);
                    }
                }

                return new ServiceResponse { Status = SRStatus.Success, Message = "customer.subscription.deleted handled." };
            }

            return new ServiceResponse { Status = SRStatus.Success, Message = "Event ignored." };
        }

        private static string? GetInvoiceSubscriptionId(Stripe.Invoice invoice)
        {
            if (!string.IsNullOrEmpty(invoice.Parent?.SubscriptionDetails?.SubscriptionId))
            {
                return invoice.Parent.SubscriptionDetails.SubscriptionId;
            }

            var lineSub = invoice.Lines?.Data?.FirstOrDefault()?.Parent?.SubscriptionItemDetails?.Subscription;
            if (!string.IsNullOrEmpty(lineSub))
            {
                return lineSub;
            }

            return null;
        }

        private static IDictionary<string, string>? ExtractInvoiceMetadata(Stripe.Invoice invoice)
        {
            if (invoice.Metadata != null && invoice.Metadata.Count > 0)
            {
                return invoice.Metadata;
            }

            if (invoice.Parent?.SubscriptionDetails?.Metadata != null &&
                invoice.Parent.SubscriptionDetails.Metadata.Count > 0)
            {
                return invoice.Parent.SubscriptionDetails.Metadata;
            }

            var lineMetadata = invoice.Lines?.Data?.FirstOrDefault()?.Metadata;
            if (lineMetadata != null && lineMetadata.Count > 0)
            {
                return lineMetadata;
            }

            return null;
        }

        private async Task<Payment?> GetPaymentFromInvoiceMetadataAsync(IDictionary<string, string>? metadata, int? fallbackCompanyId)
        {
            int paymentId = GetPaymentIdFromMetadata(metadata);
            Payment? payment = null;

            if (paymentId > 0)
            {
                payment = await _paymentRepo.GetByIdAsync(paymentId);
            }

            if (payment == null && fallbackCompanyId.HasValue)
            {
                payment = await _paymentRepo.GetLatestPendingByCompanyAsync(fallbackCompanyId.Value);
            }

            return payment;
        }

        private static int GetPaymentIdFromMetadata(IDictionary<string, string>? metadata)
        {
            if (metadata != null && metadata.TryGetValue("paymentId", out var raw))
            {
                if (int.TryParse(raw, out var paymentId))
                {
                    return paymentId;
                }
            }

            return 0;
        }

        private static async Task<string?> GetInvoiceUrlAsync(Stripe.Invoice invoice)
        {
            if (!string.IsNullOrEmpty(invoice.HostedInvoiceUrl))
                return invoice.HostedInvoiceUrl;

            if (!string.IsNullOrEmpty(invoice.InvoicePdf))
                return invoice.InvoicePdf;

            var cachedMetadataUrl = invoice.Lines?.Data?.FirstOrDefault()?.Metadata?.GetValueOrDefault("invoiceUrl");
            if (!string.IsNullOrEmpty(cachedMetadataUrl))
                return cachedMetadataUrl;

            if (!string.IsNullOrEmpty(invoice.Id))
            {
                var invoiceService = new InvoiceService();
                var refreshed = await invoiceService.GetAsync(invoice.Id);
                if (!string.IsNullOrEmpty(refreshed?.HostedInvoiceUrl))
                    return refreshed.HostedInvoiceUrl;
                if (!string.IsNullOrEmpty(refreshed?.InvoicePdf))
                    return refreshed.InvoicePdf;
            }

            return null;
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
                CreatedAt = p.CreatedAt ?? GetVietnamTime(),
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

        // ===================================================
        // 3. CANCEL SUBSCRIPTION
        // ===================================================
        public async Task<ServiceResponse> CancelSubscriptionAsync(ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            var companyUser = await _companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "You must join a company before canceling a subscription." };

            int companyId = companyUser.CompanyId.Value;

            // Tìm active subscription của company
            var companySubscription = await _companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            if (companySubscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "No active subscription found for your company."
                };
            }

            // Kiểm tra subscription đã bị cancel chưa
            if (companySubscription.SubscriptionStatus == SubscriptionStatusEnum.Canceled)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Subscription has already been canceled."
                };
            }

            // Kiểm tra có Stripe Subscription ID không
            if (string.IsNullOrWhiteSpace(companySubscription.StripeSubscriptionId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Stripe subscription ID not found. Cannot cancel subscription."
                };
            }

            try
            {
                // Gọi Stripe API để cancel subscription ngay lập tức
                var subscriptionService = new Stripe.SubscriptionService();
                var cancelOptions = new Stripe.SubscriptionCancelOptions
                {
                    InvoiceNow = true, // Cancel ngay lập tức, không chờ đến cuối billing period
                    Prorate = false // Không refund phần đã dùng
                };

                var canceledSubscription = await subscriptionService.CancelAsync(
                    companySubscription.StripeSubscriptionId,
                    cancelOptions
                );

                // Cancel ngay lập tức: mất quyền truy cập ngay
                companySubscription.SubscriptionStatus = SubscriptionStatusEnum.Canceled;
                companySubscription.IsActive = false;
                await _companySubRepo.UpdateAsync(companySubscription);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription has been canceled successfully. All access has been revoked immediately."
                };
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Stripe error canceling subscription: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to cancel subscription: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error canceling subscription: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while canceling the subscription."
                };
            }
        }


    }
}
