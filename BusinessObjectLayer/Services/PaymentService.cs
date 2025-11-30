using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Settings;
using Data.Settings.Data.Settings;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
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
        private readonly IUnitOfWork _uow;
        private readonly StripeSettings _settings;
        private readonly INotificationService _notificationService;

        public PaymentService(
            IUnitOfWork uow,
            INotificationService notificationService,
            IOptions<StripeSettings> settings)
        {
            _uow = uow;
            _notificationService = notificationService;
            _settings = settings.Value;
        }


        // ===================================================
        // 1. CREATE CHECKOUT SESSION 
        // ===================================================
        // using Stripe.Checkout; using Stripe; (ở đầu file)

        public async Task<ServiceResponse> CreateCheckoutSessionAsync(CheckoutRequest request, ClaimsPrincipal userClaims)
        {
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var paymentRepo = _uow.GetRepository<IPaymentRepository>();
            var companyRepo = _uow.GetRepository<ICompanyRepository>();
            
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "You must join a company before purchasing a subscription." };

            int companyId = companyUser.CompanyId.Value;

            var subscription = await subscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Subscription not found" };

            var existingCompanySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
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
            var company = await companyRepo.GetByIdAsync(companyId);
            if (company == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company not found" };

            var customerId = company.StripeCustomerId;
            var customerService = new CustomerService();

            if (string.IsNullOrEmpty(customerId))
            {
                // Try to set an email from user profile (falls back to null)
                var companyUserEntity = await companyUserRepo.GetCompanyUserByUserIdAsync(userId);
                string email = companyUserEntity?.User?.Email ?? null;

                var cust = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = email,
                    Metadata = new Dictionary<string, string> { { "companyId", companyId.ToString() } }
                });
                company.StripeCustomerId = cust.Id;
                await companyRepo.UpdateAsync(company);
                await _uow.SaveChangesAsync();
                customerId = cust.Id;
            }

            string domain = Environment.GetEnvironmentVariable("APPURL__CLIENTURL") ?? "http://localhost:5173";

            // Create a pending payment record before redirecting user to Stripe
            var payment = new Payment
            {
                CompanyId = companyId,
                ComSubId = null,
                PaymentStatus = PaymentStatusEnum.Pending,
            };
            await paymentRepo.AddAsync(payment);
            await _uow.SaveChangesAsync();

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

                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var paymentRepo = _uow.GetRepository<IPaymentRepository>();
                var authRepo = _uow.GetRepository<IAuthRepository>();
                
                var existing = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId);
                if (existing != null)
                {
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription already processed." };
                }
                
                var subscriptionEntity = await subscriptionRepo.GetByIdAsync(subscriptionId);
                var now = DateTime.UtcNow;

                var companySubscription = new CompanySubscription
                {
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    StartDate = now,
                    EndDate = now.AddDays(subscriptionEntity?.DurationDays ?? 0),
                    SubscriptionStatus = SubscriptionStatusEnum.Active,
                    StripeSubscriptionId = stripeSubscriptionId
                };

                await companySubRepo.AddAsync(companySubscription);
                await _uow.SaveChangesAsync();

                // Update payment with ComSubId if paymentId exists in metadata
                int.TryParse(session.Metadata?.GetValueOrDefault("paymentId") ?? "0", out int paymentId);
                if (paymentId > 0)
                {
                    var payment = await paymentRepo.GetForUpdateAsync(paymentId);
                    if (payment != null)
                    {
                        payment.ComSubId = companySubscription.ComSubId;
                        await paymentRepo.UpdateAsync(payment);
                        await _uow.SaveChangesAsync();
                    }
                }

                var admins = await authRepo.GetUsersByRoleAsync("System_Admin");
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

                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var paymentRepo = _uow.GetRepository<IPaymentRepository>();
                var transactionRepo = _uow.GetRepository<ITransactionRepository>();
                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();

                // Stripe subscription ID
                var stripeSubId =
                    invoice.Parent?.SubscriptionDetails?.SubscriptionId
                    ?? invoice.Lines?.Data.FirstOrDefault()?.Parent?.SubscriptionItemDetails?.Subscription;

                if (stripeSubId == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Missing subscription id." };

                // Tìm subscription hiện tại trong DB
                var currentSub = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubId.ToString());

                // Load metadata
                var metadata =
                    invoice.Lines?.Data?[0]?.Metadata
                    ?? invoice.Parent?.SubscriptionDetails?.Metadata
                    ?? invoice.Metadata;
                int.TryParse(metadata?.GetValueOrDefault("companyId"), out int companyId);
                int.TryParse(metadata?.GetValueOrDefault("subscriptionId"), out int subscriptionId);
                int.TryParse(metadata?.GetValueOrDefault("paymentId"), out int initialPaymentId);

                var subscriptionDef = await subscriptionRepo.GetByIdAsync(subscriptionId);
                if (subscriptionDef == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Subscription definition missing." };

                // Stripe timestamp chuẩn
                var line = invoice?.Lines?.Data.FirstOrDefault();
                var periodStart = line?.Period?.Start;
                var periodEnd = line?.Period?.End;

                // Determine if this is renewal or initial payment
                bool isInitial = invoice.BillingReason == "subscription_create";
                bool isRenewal = invoice.BillingReason == "subscription_cycle";

                bool isCanceledBefore = currentSub != null && currentSub.SubscriptionStatus == SubscriptionStatusEnum.Canceled;

                if (isCanceledBefore)
                {
                    // Không tạo renew
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription was canceled; renewal ignored." };
                }

                CompanySubscription newSub = null;

                if (isInitial)
                {
                    // Initial payment
                    newSub = new CompanySubscription
                    {
                        CompanyId = companyId,
                        SubscriptionId = subscriptionId,
                        StripeSubscriptionId = stripeSubId,
                        StartDate = periodStart.Value,
                        EndDate = periodEnd.Value,
                        SubscriptionStatus = SubscriptionStatusEnum.Active
                    };
                    await companySubRepo.AddAsync(newSub);
                    await _uow.SaveChangesAsync();
                }
                else if (isRenewal)
                {
                    // Renewal
                    currentSub.SubscriptionStatus = SubscriptionStatusEnum.Expired;
                    await companySubRepo.UpdateAsync(currentSub);
                    await _uow.SaveChangesAsync();

                    newSub = new CompanySubscription
                    {
                        CompanyId = companyId,
                        SubscriptionId = subscriptionId,
                        StripeSubscriptionId = stripeSubId,
                        StartDate = periodStart.Value,
                        EndDate = periodEnd.Value,
                        SubscriptionStatus = SubscriptionStatusEnum.Active
                    };

                    await companySubRepo.AddAsync(newSub);
                    await _uow.SaveChangesAsync();
                }

                // Tạo Payment mới
                var payment = new Payment
                {
                    CompanyId = companyId,
                    ComSubId = newSub.ComSubId,
                    PaymentStatus = PaymentStatusEnum.Paid,
                    InvoiceUrl = invoice.HostedInvoiceUrl
                };
                await paymentRepo.AddAsync(payment);
                await _uow.SaveChangesAsync();

                // Tạo Transaction mới
                await transactionRepo.AddAsync(new Transaction
                {
                    PaymentId = payment.PaymentId,
                    Amount = (int)invoice.AmountPaid,
                    Currency = invoice.Currency,
                    Gateway = TransactionGatewayEnum.StripePayment,
                    ResponseCode = "SUCCESS",
                    ResponseMessage = isInitial ? "Initial payment" : "Renewal payment",
                    TransactionTime = DateTime.UtcNow
                });

                await _uow.SaveChangesAsync();

                return new ServiceResponse { Status = SRStatus.Success, Message = "invoice.payment_succeeded processed." };
            }

            // ============================================================
            // 3) invoice.payment_failed
            // ============================================================
            if (stripeEvent.Type == "invoice.payment_failed")
            {
                var invoice = stripeEvent.Data.Object as Stripe.Invoice;
                if (invoice != null)
                {
                    var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                    var paymentRepo = _uow.GetRepository<IPaymentRepository>();
                    
                    var stripeSubId = GetInvoiceSubscriptionId(invoice);
                    if (string.IsNullOrEmpty(stripeSubId))
                    {
                        return new ServiceResponse { Status = SRStatus.Error, Message = "Invoice missing subscription id." };
                    }

                    var companySub = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubId);
                    if (companySub != null)
                    {
                        companySub.SubscriptionStatus = SubscriptionStatusEnum.Pending;
                        await companySubRepo.UpdateAsync(companySub);
                        await _uow.SaveChangesAsync();
                    }

                    var metadataSnapshot = ExtractInvoiceMetadata(invoice);
                    var payment = await GetPaymentFromInvoiceMetadataAsync(metadataSnapshot, companySub?.CompanyId);
                    if (payment != null)
                    {
                        payment.PaymentStatus = PaymentStatusEnum.Failed;
                        await paymentRepo.UpdateAsync(payment);
                        await _uow.SaveChangesAsync();
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

                    var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                    var cs = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeId);
                    if (cs != null)
                    {
                        cs.SubscriptionStatus = SubscriptionStatusEnum.Canceled;
                        await companySubRepo.UpdateAsync(cs);
                        await _uow.SaveChangesAsync();
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
            var paymentRepo = _uow.GetRepository<IPaymentRepository>();
            int paymentId = GetPaymentIdFromMetadata(metadata);
            Payment? payment = null;

            if (paymentId > 0)
            {
                payment = await paymentRepo.GetForUpdateAsync(paymentId);
            }

            if (payment == null && fallbackCompanyId.HasValue)
            {
                payment = await paymentRepo.GetLatestPendingByCompanyAsync(fallbackCompanyId.Value);
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





        public async Task<ServiceResponse> GetPaymentsAsync(ClaimsPrincipal userClaims)
        {
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var paymentRepo = _uow.GetRepository<IPaymentRepository>();
            
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            int userId = int.Parse(userIdClaim);

            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "User is not associated with any company." };

            int companyId = companyUser.CompanyId.Value;

            var payments = await paymentRepo.GetPaymentsByCompanyAsync(companyId);

            var responseList = payments.Select(p =>
            {
                var transaction = p.Transactions?.OrderByDescending(t => t.TransactionTime).FirstOrDefault();
                return new PaymentListResponse
                {
                    PaymentId = p.PaymentId,
                    CompanyId = p.CompanyId,
                    ComSubId = p.ComSubId,
                    PaymentStatus = p.PaymentStatus,
                    SubscriptionStatus = p.CompanySubscription?.SubscriptionStatus,
                    Amount = transaction?.Amount ?? 0,
                    Currency = transaction?.Currency,
                    SubscriptionName = p.CompanySubscription?.Subscription?.Name,
                    StartDate = p.CompanySubscription?.StartDate,
                    EndDate = p.CompanySubscription?.EndDate,
                    TransactionTime = transaction?.TransactionTime
                };
            })
            .ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Payments retrieved successfully.",
                Data = responseList
            };
        }

        public async Task<ServiceResponse> GetPaymentDetailAsync(ClaimsPrincipal userClaims, int paymentId)
        {
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var paymentRepo = _uow.GetRepository<IPaymentRepository>();
            
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            int userId = int.Parse(userIdClaim);

            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "User is not associated with any company." };

            int companyId = companyUser.CompanyId.Value;

            var payment = await paymentRepo.GetPaymentDetailByIdAsync(paymentId, companyId);
            if (payment == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Payment not found." };

            var transaction = payment.Transactions?.OrderByDescending(t => t.TransactionTime).FirstOrDefault();
            
            var response = new PaymentDetailResponse
            {
                PaymentId = payment.PaymentId,
                CompanyId = payment.CompanyId,
                ComSubId = payment.ComSubId,
                PaymentStatus = payment.PaymentStatus,
                InvoiceUrl = payment.InvoiceUrl,
                Transaction = transaction != null ? new TransactionDetail
                {
                    TransactionId = transaction.TransactionId,
                    TransactionRef = transaction.TransactionRef,
                    Gateway = transaction.Gateway,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    PayerName = transaction.PayerName,
                    BankCode = transaction.BankCode,
                    TransactionTime = transaction.TransactionTime
                } : null,
                CompanySubscription = payment.CompanySubscription != null ? new CompanySubscriptionDetail
                {
                    SubscriptionId = payment.CompanySubscription.SubscriptionId,
                    Name = payment.CompanySubscription.Subscription?.Name ?? string.Empty,
                    StartDate = payment.CompanySubscription.StartDate,
                    EndDate = payment.CompanySubscription.EndDate,
                    Status = payment.CompanySubscription.SubscriptionStatus
                } : null
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Payment detail retrieved successfully.",
                Data = response
            };
        }

        public async Task<ServiceResponse> GetPaymentBySessionIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Session ID is required."
                };
            }

            try
            {
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(sessionId, new SessionGetOptions
                {
                    Expand = new List<string> { "subscription" }
                });

                if (session == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Stripe session not found."
                    };
                }

                var paymentRepo = _uow.GetRepository<IPaymentRepository>();
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();

                // Get payment from metadata
                int.TryParse(session.Metadata?.GetValueOrDefault("paymentId") ?? "0", out int paymentId);
                int.TryParse(session.Metadata?.GetValueOrDefault("companyId") ?? "0", out int companyId);

                Payment? payment = null;
                if (paymentId > 0)
                {
                    // Try with companyId first if available
                    if (companyId > 0)
                    {
                        payment = await paymentRepo.GetPaymentDetailByIdAsync(paymentId, companyId);
                    }
                    // Fallback: get payment with transactions without companyId filter
                    if (payment == null)
                    {
                        payment = await paymentRepo.GetByIdWithTransactionsAsync(paymentId);
                    }
                }
                else if (companyId > 0)
                {
                    // Fallback: get latest pending payment for company
                    payment = await paymentRepo.GetLatestPendingByCompanyAsync(companyId);
                    // Load transactions if payment found
                    if (payment != null && payment.Transactions == null)
                    {
                        payment = await paymentRepo.GetByIdWithTransactionsAsync(payment.PaymentId);
                    }
                }

                if (payment == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Payment not found for this session."
                    };
                }

                // Get CompanySubscription if exists
                CompanySubscription? companySub = null;
                string? stripeSubscriptionId = null;

                if (session.Subscription != null)
                {
                    if (session.Subscription is Stripe.Subscription subObject)
                    {
                        stripeSubscriptionId = subObject.Id;
                    }
                    else
                    {
                        stripeSubscriptionId = session.Subscription.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(stripeSubscriptionId))
                {
                    companySub = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId);
                }
                else if (payment.ComSubId.HasValue)
                {
                    // Try to get by ComSubId if available
                    companySub = await companySubRepo.GetByIdAsync(payment.ComSubId.Value);
                }

                var subscriptionName = companySub?.Subscription?.Name;

                // Get the latest transaction
                var transaction = payment.Transactions?.OrderByDescending(t => t.TransactionTime).FirstOrDefault();

                var response = new PaymentSessionResponse
                {
                    PaymentId = payment.PaymentId,
                    CompanyId = payment.CompanyId,
                    PaymentStatus = payment.PaymentStatus,
                    InvoiceUrl = payment.InvoiceUrl,
                    SessionStatus = session.Status ?? "unknown",
                    StripeSubscriptionId = stripeSubscriptionId,
                    ComSubId = payment.ComSubId ?? companySub?.ComSubId,
                    SubscriptionName = subscriptionName,
                    Transaction = transaction != null ? new TransactionResponse
                    {
                        TransactionId = transaction.TransactionId,
                        TransactionRef = transaction.TransactionRef,
                        Gateway = transaction.Gateway,
                        Amount = transaction.Amount,
                        Currency = transaction.Currency,
                        PayerName = transaction.PayerName,
                        BankCode = transaction.BankCode,
                        TransactionTime = transaction.TransactionTime
                    } : null
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Payment session retrieved successfully.",
                    Data = response
                };
            }
            catch (StripeException ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Stripe error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error retrieving payment session: {ex.Message}"
                };
            }
        }

        // ===================================================
        // 3. CANCEL SUBSCRIPTION
        // ===================================================
        public async Task<ServiceResponse> CancelSubscriptionAsync(ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            
            var userId = int.Parse(userIdClaim);

            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "You must join a company before canceling a subscription." };

            int companyId = companyUser.CompanyId.Value;

            // Tìm active subscription của company
            var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
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
                await companySubRepo.UpdateAsync(companySubscription);
                await _uow.SaveChangesAsync();

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

        // ===================================================
        // 4. GET CURRENT SUBSCRIPTION
        // ===================================================
        public async Task<ServiceResponse> GetCurrentSubscriptionAsync(ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            
            var userId = int.Parse(userIdClaim);

            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Error, Message = "You must join a company to view subscription." };

            int companyId = companyUser.CompanyId.Value;

            // Lấy subscription hiện tại (Active hoặc Pending và chưa hết hạn)
            var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            
            if (companySubscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "No active subscription found for your company.",
                    Data = null
                };
            }

            var response = new CurrentSubscriptionResponse
            {
                SubscriptionName = companySubscription.Subscription?.Name ?? string.Empty,
                Description = companySubscription.Subscription?.Description,
                Price = companySubscription.Subscription?.Price ?? 0,
                DurationDays = companySubscription.Subscription?.DurationDays ?? 0,
                ResumeLimit = companySubscription.Subscription?.ResumeLimit ?? 0,
                HoursLimit = companySubscription.Subscription?.HoursLimit ?? 0,
                StartDate = companySubscription.StartDate,
                EndDate = companySubscription.EndDate,
                SubscriptionStatus = companySubscription.SubscriptionStatus
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Current subscription retrieved successfully.",
                Data = response
            };
        }
    }
}