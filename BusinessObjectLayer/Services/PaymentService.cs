using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Services.Auth;
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
using Microsoft.Extensions.Logging;
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
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IUnitOfWork uow,
            INotificationService notificationService,
            IOptions<StripeSettings> settings,
            IEmailService emailService,
            ILogger<PaymentService> logger)
        {
            _uow = uow;
            _notificationService = notificationService;
            _settings = settings.Value;
            _emailService = emailService;
            _logger = logger;
        }


        // ===================================================
        // 1. CREATE CHECKOUT SESSION 
        // ===================================================
       

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

            // Kiểm tra subscription Free không được thanh toán
            var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
            if (freeSubscription != null && subscription.SubscriptionId == freeSubscription.SubscriptionId)
            {
                return new ServiceResponse 
                { 
                    Status = SRStatus.Validation, 
                    Message = "Free subscription cannot be purchased. It is automatically assigned to companies." 
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
            
            // Get user email
            var companyUserEntity = await companyUserRepo.GetCompanyUserByUserIdAsync(userId);
            string userEmail = companyUserEntity?.User?.Email;

            if (string.IsNullOrEmpty(customerId))
            {
                // Create new customer with email
                var cust = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = userEmail,
                    Metadata = new Dictionary<string, string> { { "companyId", companyId.ToString() } }
                });
                company.StripeCustomerId = cust.Id;
                await companyRepo.UpdateAsync(company);
                await _uow.SaveChangesAsync();
                customerId = cust.Id;
            }
            else
            {
                // Customer already exists - update email if missing or different
                var existingCustomer = await customerService.GetAsync(customerId);
                if (existingCustomer != null && !string.IsNullOrWhiteSpace(userEmail))
                {
                    // Update email if customer doesn't have one or if it's different
                    if (string.IsNullOrWhiteSpace(existingCustomer.Email) || 
                        !existingCustomer.Email.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        var updateOptions = new CustomerUpdateOptions
                        {
                            Email = userEmail
                        };
                        await customerService.UpdateAsync(customerId, updateOptions);
                    }
                }
            }

            // Check if company already has an active subscription
            var activeSubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            if (activeSubscription != null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Your company already has an active subscription. Please wait until it expires before purchasing a new one."
                };
            }

            // Check if there's already a pending payment for this company
            // var existingPendingPayment = await paymentRepo.GetLatestPendingByCompanyAsync(companyId);
            // if (existingPendingPayment != null)
            // {
            //     return new ServiceResponse
            //     {
            //         Status = SRStatus.Error,
            //         Message = "You already have a pending payment session. Please complete or cancel the existing payment before creating a new one."
            //     };
            // }

            string domain = Environment.GetEnvironmentVariable("APPURL__CLIENTURL") ?? "https://aices-client.vercel.app";

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

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                CustomerEmail = userEmail, // Ensure email is passed to checkout for receipt delivery
                SuccessUrl = $"{domain}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/subscriptions",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(31).UtcDateTime,
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

            _logger.LogDebug("Checkout session expiration - ExpiresAt: {ExpiresAt}, UTC Now: {UtcNow}, Local Time: {LocalTime}, Unix: {UnixTime}", 
                options.ExpiresAt, DateTime.UtcNow, DateTime.Now, DateTimeOffset.UtcNow.ToUnixTimeSeconds());


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
                        _logger.LogWarning(ex, "Error retrieving session with expand for session {SessionId}", session.Id);
                    }
                }

                if (string.IsNullOrEmpty(stripeSubscriptionId))
                {
                    // Trả về Error để Stripe biết có vấn đề và có thể retry
                    // Hoặc log để debug vì đây là trường hợp không bình thường
                    _logger.LogError("checkout.session.completed - Session {SessionId} has no subscription ID. Session mode: {Mode}", session.Id, session.Mode);
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Session has no subscription id. Cannot process payment." };
                }

                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();

                // ========== Check if this subscription was already processed (idempotency check) ==========
                // IMPORTANT: Check this FIRST before duplicate check to prevent reprocessing the same subscription
                var existing = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId);
                if (existing != null)
                {
                    _logger.LogInformation("Subscription {StripeSubscriptionId} already processed (ComSubId: {ComSubId}). Returning success.", stripeSubscriptionId, existing.ComSubId);
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription already processed." };
                }

                // ========== prevent duplicate active subscriptions ==========
                // Check for existing active subscription in database (different from the new one)
                var existingActive = await companySubRepo.GetAnyActiveSubscriptionByCompanyForPaymentAsync(companyId);
                if (existingActive != null)
                {
                    // CRITICAL FIX: Verify this is NOT the same subscription we're processing
                    // If Stripe IDs match, it means we're processing the same subscription (duplicate webhook or race condition)
                    if (existingActive.StripeSubscriptionId == stripeSubscriptionId)
                    {
                        _logger.LogDebug("Found existing subscription with same Stripe ID {StripeSubscriptionId}. This is likely a duplicate webhook call. Returning success.", stripeSubscriptionId);
                        return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription already processed." };
                    }

                    // Log thông tin subscription đang được coi là active để debug
                    _logger.LogDebug("Found existing active subscription for company {CompanyId}: ComSubId={ComSubId}, Status={Status}, IsActive={IsActive}, EndDate={EndDate}, StripeSubId={StripeSubId}",
                        companyId, existingActive.ComSubId, existingActive.SubscriptionStatus, existingActive.IsActive, existingActive.EndDate, existingActive.StripeSubscriptionId);
                    
                    // Check if the existing subscription is actually valid in Stripe
                    if (!string.IsNullOrEmpty(existingActive.StripeSubscriptionId))
                    {
                        try
                        {
                            var subscriptionService = new Stripe.SubscriptionService();
                            var stripeSub = await subscriptionService.GetAsync(existingActive.StripeSubscriptionId);
                            _logger.LogDebug("Stripe subscription status: {Status} for subscription {StripeSubscriptionId}", stripeSub.Status, existingActive.StripeSubscriptionId);
                            
                            // If Stripe subscription is already canceled/expired/unpaid, update DB and allow new subscription
                            if (stripeSub.Status == "canceled" || stripeSub.Status == "unpaid" || stripeSub.Status == "past_due" || stripeSub.Status == "incomplete_expired")
                            {
                                _logger.LogDebug("Existing subscription in Stripe is {Status}. Updating DB and allowing new subscription.", stripeSub.Status);
                                
                                // Update existing subscription status in DB to match Stripe
                                existingActive.SubscriptionStatus = stripeSub.Status == "canceled" 
                                    ? SubscriptionStatusEnum.Canceled 
                                    : SubscriptionStatusEnum.Expired;
                                await companySubRepo.UpdateAsync(existingActive);
                                await _uow.SaveChangesAsync();
                                
                                _logger.LogInformation("Updated existing subscription {ComSubId} status to {Status}. Proceeding with new subscription.", existingActive.ComSubId, existingActive.SubscriptionStatus);
                                // Continue with new subscription creation
                            }
                            else if (stripeSub.Status == "active" || stripeSub.Status == "trialing")
                            {
                                // Existing subscription is still active in Stripe - this is a real duplicate (OLD subscription)
                                // CRITICAL: Verify we're not canceling the NEW subscription by checking IDs don't match
                                if (existingActive.StripeSubscriptionId != stripeSubscriptionId)
                                {
                                    // Cancel the OLD subscription (not the new one that was just paid for)
                                    _logger.LogInformation("Real duplicate detected. Canceling OLD subscription {OldStripeSubscriptionId} (different from new {NewStripeSubscriptionId}).", existingActive.StripeSubscriptionId, stripeSubscriptionId);
                                    
                                    try
                                    {
                                        await subscriptionService.CancelAsync(existingActive.StripeSubscriptionId, new SubscriptionCancelOptions
                                        {
                                            InvoiceNow = false,
                                            Prorate = false
                                        });
                                        
                                        // Update DB status
                                        existingActive.SubscriptionStatus = SubscriptionStatusEnum.Canceled;
                                        await companySubRepo.UpdateAsync(existingActive);
                                        await _uow.SaveChangesAsync();
                                        
                                        _logger.LogInformation("Canceled old subscription {OldStripeSubscriptionId}. New subscription {NewStripeSubscriptionId} will proceed.", existingActive.StripeSubscriptionId, stripeSubscriptionId);
                                        // Continue with new subscription creation
                                    }
                                    catch (Exception cancelEx)
                                    {
                                        _logger.LogError(cancelEx, "Failed to cancel old subscription {StripeSubscriptionId}", existingActive.StripeSubscriptionId);
                                        // Continue anyway - don't block the new subscription that was just paid for
                                    }
                                }
                                else
                                {
                                    // This should not happen due to the check above, but adding as safety
                                    _logger.LogWarning("Attempted to cancel subscription with same ID. This is a bug! StripeSubId: {StripeSubscriptionId}", stripeSubscriptionId);
                                    return new ServiceResponse { Status = SRStatus.Success, Message = "Subscription already processed." };
                                }
                            }
                            else
                            {
                                // Unknown status - log and allow new subscription to proceed
                                _logger.LogDebug("Existing subscription has unknown status {Status}. Allowing new subscription.", stripeSub.Status);
                            }
                        }
                        catch (StripeException stripeEx)
                        {
                            // If subscription not found in Stripe, it means it was already deleted
                            if (stripeEx.StripeError?.Code == "resource_missing")
                            {
                                _logger.LogDebug("Existing subscription {StripeSubscriptionId} not found in Stripe. Updating DB and allowing new subscription.", existingActive.StripeSubscriptionId);
                                
                                // Update DB to mark as expired/canceled
                                existingActive.SubscriptionStatus = SubscriptionStatusEnum.Expired;
                                await companySubRepo.UpdateAsync(existingActive);
                                await _uow.SaveChangesAsync();
                                
                                // Continue with new subscription creation
                            }
                            else
                            {
                                _logger.LogError(stripeEx, "Stripe error checking subscription {StripeSubscriptionId}", existingActive.StripeSubscriptionId);
                                // Continue anyway to avoid blocking the new subscription that was just paid for
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error checking Stripe subscription {StripeSubscriptionId}", existingActive.StripeSubscriptionId);
                            // Continue anyway to avoid blocking the new subscription that was just paid for
                        }
                    }
                    else
                    {
                        // No Stripe subscription ID in DB - this is an orphaned record
                        // Mark it as expired and allow new subscription
                        _logger.LogDebug("Existing subscription {ComSubId} has no Stripe ID. Marking as expired and allowing new subscription.", existingActive.ComSubId);
                        
                        existingActive.SubscriptionStatus = SubscriptionStatusEnum.Expired;
                        await companySubRepo.UpdateAsync(existingActive);
                        await _uow.SaveChangesAsync();
                        
                        // Continue with new subscription creation
                    }
                }
                else
                {
                    // Log để confirm không có active subscription
                    _logger.LogDebug("No active subscription found for company {CompanyId}. Proceeding with new subscription creation.", companyId);
                }

                var paymentRepo = _uow.GetRepository<IPaymentRepository>();
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                
                var subscriptionEntity = await subscriptionRepo.GetByIdAsync(subscriptionId);
                var company = await companyRepo.GetByIdAsync(companyId);
                var now = DateTime.UtcNow;

                var companySubscription = new CompanySubscription
                {
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    StartDate = now,
                    EndDate = subscriptionEntity != null ? subscriptionEntity.Duration.CalculateEndDate(now) : now.AddMonths(1),
                    SubscriptionStatus = SubscriptionStatusEnum.Active,
                    StripeSubscriptionId = stripeSubscriptionId
                };

                await companySubRepo.AddAsync(companySubscription);
                await _uow.SaveChangesAsync();

                // ✅ FIX: DO NOT reset usage counters when new subscription is activated
                // Reason 1: Preserve usage history for dashboard tracking
                // Reason 2: Prevent credit loss when upgrading mid-period (e.g., used 3/3 free, upgrade to 10 paid → should get 7 remaining, not reset to 0)
                // The GetOrCreateCounterAsync method will automatically handle:
                // - Creating new counters for new periods
                // - Updating limits for existing counters in same period
                // - Old counters will naturally expire based on their period dates

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

                // Send notifications to company members with type Subscription
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyMembers = await companyUserRepo.GetApprovedAndInvitedMembersByCompanyIdAsync(companyId);
                foreach (var member in companyMembers)
                {
                    if (member.User != null)
                    {
                        await _notificationService.CreateAsync(
                            member.User.UserId,
                            NotificationTypeEnum.Subscription,
                            "Subscription activated",
                            $"Your company has successfully subscribed to {subscriptionEntity?.Name ?? "a subscription plan"}."
                        );
                    }
                }

                // Send notifications to system roles (roleId = 1,2,3) with type SystemSubscription
                var systemRoles = new[] { "System_Admin", "System_Manager", "System_Staff" };
                foreach (var roleName in systemRoles)
                {
                    var systemUsers = await authRepo.GetUsersByRoleAsync(roleName);
                    foreach (var systemUser in systemUsers)
                    {
                        await _notificationService.CreateAsync(
                            systemUser.UserId,
                            NotificationTypeEnum.SystemSubscription,
                            "Company subscription",
                            $"Company {company?.Name ?? companyId.ToString()} has subscribed to {subscriptionEntity?.Name ?? "a subscription plan"}."
                        );
                    }
                }

                // Expire all other open sessions for the same customer
                try
                {
                    var sessionService = new SessionService();
                    string? customerId = null;
                    
                    // Get customer ID from session
                    if (session.Customer != null)
                    {
                        if (session.Customer is Stripe.Customer customerObj)
                        {
                            customerId = customerObj.Id;
                        }
                        else
                        {
                            customerId = session.Customer.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(customerId))
                    {
                        // List all open sessions for this customer
                        var sessionListOptions = new SessionListOptions
                        {
                            Customer = customerId,
                            Status = "open" // Only get open sessions
                        };
                        
                        var allSessions = await sessionService.ListAsync(sessionListOptions);
                        
                        // Expire all other sessions (excluding the current completed one)
                        int expiredCount = 0;
                        foreach (var otherSession in allSessions.Data)
                        {
                            if (otherSession.Id != session.Id && otherSession.Status == "open")
                            {
                                try
                                {
                                    await sessionService.ExpireAsync(otherSession.Id);
                                    expiredCount++;
                                    _logger.LogInformation("Expired session {SessionId} for customer {CustomerId}", otherSession.Id, customerId);
                                }
                                catch (Exception ex)
                                {
                                    // Log but don't fail the webhook
                                    _logger.LogError(ex, "Failed to expire session {SessionId} for customer {CustomerId}", otherSession.Id, customerId);
                                }
                            }
                        }
                        
                        if (expiredCount > 0)
                        {
                            _logger.LogInformation("Expired {ExpiredCount} other session(s) for customer {CustomerId} after successful payment", expiredCount, customerId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the webhook - this is a cleanup operation
                    _logger.LogError(ex, "Failed to expire other sessions for customer {CustomerId}", session.Customer?.ToString());
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

                CompanySubscription targetSub = null;
                Payment payment = null;

                if (isInitial)
                {
                    // Initial payment - check if subscription already created by checkout.session.completed
                    var existingSub = await companySubRepo.GetByStripeSubscriptionIdAsync(stripeSubId);
                    if (existingSub != null)
                    {
                        // Subscription already exists from checkout.session.completed
                        targetSub = existingSub;
                        // Update dates from invoice if needed
                        if (periodStart.HasValue) targetSub.StartDate = periodStart.Value;
                        if (periodEnd.HasValue) targetSub.EndDate = periodEnd.Value;
                        targetSub.SubscriptionStatus = SubscriptionStatusEnum.Active;
                        await companySubRepo.UpdateAsync(targetSub);
                        await _uow.SaveChangesAsync();
                    }
                    else
                    {
                        // Create new subscription if not exists
                        // Use Stripe period dates if available, otherwise calculate from subscription Duration
                        var startDate = periodStart ?? DateTime.UtcNow;
                        var endDate = periodEnd;
                        if (!endDate.HasValue && subscriptionDef != null)
                        {
                            endDate = subscriptionDef.Duration.CalculateEndDate(startDate);
                        }
                        
                        targetSub = new CompanySubscription
                        {
                            CompanyId = companyId,
                            SubscriptionId = subscriptionId,
                            StripeSubscriptionId = stripeSubId,
                            StartDate = startDate,
                            EndDate = endDate ?? (subscriptionDef != null ? subscriptionDef.Duration.CalculateEndDate(startDate) : startDate.AddMonths(1)),
                            SubscriptionStatus = SubscriptionStatusEnum.Active
                        };
                        await companySubRepo.AddAsync(targetSub);
                        await _uow.SaveChangesAsync();
                    }

                    // For initial payment, find and update existing payment from checkout
                    if (initialPaymentId > 0)
                    {
                        payment = await paymentRepo.GetForUpdateAsync(initialPaymentId);
                    }
                    
                    // Fallback: get latest pending payment for company
                    if (payment == null && companyId > 0)
                    {
                        payment = await paymentRepo.GetLatestPendingByCompanyAsync(companyId);
                    }

                    if (payment != null)
                    {
                        // Update existing payment
                        payment.ComSubId = targetSub.ComSubId;
                        payment.PaymentStatus = PaymentStatusEnum.Paid;
                        payment.InvoiceUrl = invoice.HostedInvoiceUrl;
                        await paymentRepo.UpdateAsync(payment);
                        await _uow.SaveChangesAsync();
                    }
                    else
                    {
                        // Create new payment only if no existing one found
                        payment = new Payment
                        {
                            CompanyId = companyId,
                            ComSubId = targetSub.ComSubId,
                            PaymentStatus = PaymentStatusEnum.Paid,
                            InvoiceUrl = invoice.HostedInvoiceUrl
                        };
                        await paymentRepo.AddAsync(payment);
                        await _uow.SaveChangesAsync();
                    }
                }
                else if (isRenewal)
                {
                    // Renewal
                    if (currentSub == null)
                    {
                        _logger.LogWarning("Renewal invoice received but no existing subscription found for Stripe ID {StripeSubId}", stripeSubId);
                        return new ServiceResponse { Status = SRStatus.Error, Message = "Cannot process renewal: existing subscription not found." };
                    }

                    currentSub.SubscriptionStatus = SubscriptionStatusEnum.Expired;
                    await companySubRepo.UpdateAsync(currentSub);
                    await _uow.SaveChangesAsync();

                    // Use Stripe period dates if available, otherwise calculate from subscription Duration
                    var startDate = periodStart ?? DateTime.UtcNow;
                    var endDate = periodEnd;
                    if (!endDate.HasValue && subscriptionDef != null)
                    {
                        endDate = subscriptionDef.Duration.CalculateEndDate(startDate);
                    }

                    targetSub = new CompanySubscription
                    {
                        CompanyId = companyId,
                        SubscriptionId = subscriptionId,
                        StripeSubscriptionId = stripeSubId,
                        StartDate = startDate,
                        EndDate = endDate ?? (subscriptionDef != null ? subscriptionDef.Duration.CalculateEndDate(startDate) : startDate.AddMonths(1)),
                        SubscriptionStatus = SubscriptionStatusEnum.Active
                    };

                    await companySubRepo.AddAsync(targetSub);
                    await _uow.SaveChangesAsync();

                    // For renewal, always create new payment
                    payment = new Payment
                    {
                        CompanyId = companyId,
                        ComSubId = targetSub.ComSubId,
                        PaymentStatus = PaymentStatusEnum.Paid,
                        InvoiceUrl = invoice.HostedInvoiceUrl
                    };
                    await paymentRepo.AddAsync(payment);
                    await _uow.SaveChangesAsync();
                }

                // Tạo Transaction mới nếu payment được xử lý
                if (payment != null)
                {
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
                }

                // Gửi receipt email - tự động lấy email từ nhiều nguồn
                if (invoice.AmountPaid > 0)
                {
                    try
                    {
                        // Lấy email từ nhiều nguồn (ưu tiên từ cao đến thấp)
                        string? customerEmail = invoice.CustomerEmail;
                        
                        // Fallback 1: Lấy từ Stripe Customer nếu invoice không có email
                        if (string.IsNullOrWhiteSpace(customerEmail) && invoice.Customer != null)
                        {
                            try
                            {
                                var customerService = new CustomerService();
                                string customerId = invoice.Customer is Stripe.Customer custObj ? custObj.Id : invoice.Customer.ToString();
                                var customer = await customerService.GetAsync(customerId);
                                customerEmail = customer?.Email;
                                _logger.LogDebug("Retrieved email from Stripe Customer: {Email}", customerEmail);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not retrieve email from Stripe Customer for invoice {InvoiceId}", invoice.Id);
                            }
                        }
                        
                        // Fallback 2: Lấy từ database CompanyUser nếu vẫn không có email
                        if (string.IsNullOrWhiteSpace(customerEmail) && companyId > 0)
                        {
                            try
                            {
                                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                                
                                // Ưu tiên lấy HR Manager đầu tiên
                                var hrManagers = await companyUserRepo.GetHrManagersByCompanyIdAsync(companyId);
                                var firstHrManager = hrManagers?.FirstOrDefault();
                                
                                if (firstHrManager?.User?.Email != null)
                                {
                                    customerEmail = firstHrManager.User.Email;
                                    _logger.LogDebug("Retrieved email from HR Manager: {Email}", customerEmail);
                                }
                                else
                                {
                                    // Fallback: lấy approved member đầu tiên
                                    var approvedMembers = await companyUserRepo.GetApprovedAndInvitedMembersByCompanyIdAsync(companyId);
                                    var firstMember = approvedMembers?.FirstOrDefault();
                                    customerEmail = firstMember?.User?.Email;
                                    _logger.LogDebug("Retrieved email from first approved member: {Email}", customerEmail);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not retrieve email from database for company {CompanyId}", companyId);
                            }
                        }
                        
                        // Gửi email nếu đã lấy được
                        if (!string.IsNullOrWhiteSpace(customerEmail))
                        {
                            // Lấy subscription name từ metadata hoặc subscription entity
                            string subscriptionName = subscriptionDef?.Name ?? "Subscription";
                            decimal amountInDollars = (decimal)invoice.AmountPaid / 100; // Convert from cents to dollars
                            string invoiceUrl = invoice.HostedInvoiceUrl ?? invoice.InvoicePdf ?? "";
                            
                            // Lấy receipt number từ invoice number
                            string receiptNumber = invoice.Number ?? invoice.Id?.Replace("in_", "").Substring(0, Math.Min(8, invoice.Id?.Length ?? 8)) ?? "0000-0000";
                            if (receiptNumber.Length > 8) receiptNumber = receiptNumber.Substring(0, 8);
                            if (receiptNumber.Length >= 4) receiptNumber = receiptNumber.Insert(4, "-");
                            
                            
                            string paymentMethod = "Card";
                            
                            // Lấy date paid từ invoice
                            DateTime? datePaid = invoice.StatusTransitions?.PaidAt ?? invoice.EffectiveAt;

                            await _emailService.SendReceiptEmailAsync(
                                customerEmail,
                                invoiceUrl,
                                amountInDollars,
                                invoice.Currency?.ToUpper() ?? "USD",
                                subscriptionName,
                                receiptNumber,
                                paymentMethod,
                                datePaid
                            );
                            _logger.LogInformation("Receipt email sent for invoice {InvoiceId} to {CustomerEmail}", invoice.Id, customerEmail);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find customer email for invoice {InvoiceId}. Receipt email not sent.", invoice.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error nhưng không fail webhook
                        _logger.LogError(ex, "Error sending receipt email for invoice {InvoiceId}", invoice.Id);
                    }
                }

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
            try
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving payments: {Message}", ex.Message);
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving payments.",
                    Data = ex.Message
                };
            }
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
                    Message = "No active paid subscription found. You are currently on the Free plan which cannot be canceled."
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

            // Kiểm tra nếu là Free subscription thì không cho hủy (chỉ có thể hủy subscription trả phí)
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscription = await subscriptionRepo.GetByIdAsync(companySubscription.SubscriptionId);
            var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
            if (freeSubscription != null && subscription != null && subscription.SubscriptionId == freeSubscription.SubscriptionId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Free subscription cannot be canceled. It is the default subscription."
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

                // ✅ FIX: DO NOT reset usage counters when subscription is canceled
                // Reason: We need to preserve usage history for dashboard tracking
                // The usage limit will automatically switch to Free plan limits based on active subscription check
                // Old counters will naturally expire based on their period dates

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription has been canceled successfully. You are now on the Free plan."
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error canceling subscription {StripeSubscriptionId}", companySubscription.StripeSubscriptionId);
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to cancel subscription: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling subscription {StripeSubscriptionId}", companySubscription.StripeSubscriptionId);
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
            
            CurrentSubscriptionResponse response;
            
            if (companySubscription == null)
            {
                // Không có subscription active, trả về Free subscription
                var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
                
                if (freeSubscription == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No active subscription found and Free subscription not configured.",
                        Data = null
                    };
                }
                
                response = new CurrentSubscriptionResponse
                {
                    SubscriptionName = freeSubscription.Name,
                    Description = freeSubscription.Description,
                    Price = freeSubscription.Price,
                    Duration = freeSubscription.Duration,
                    ResumeLimit = freeSubscription.ResumeLimit,
                    HoursLimit = freeSubscription.HoursLimit,
                    CompareLimit = freeSubscription.CompareLimit,
                    CompareHoursLimit = freeSubscription.CompareHoursLimit,
                    StartDate = null,
                    EndDate = null,
                    SubscriptionStatus = SubscriptionStatusEnum.Active
                };
            }
            else
            {
                response = new CurrentSubscriptionResponse
                {
                    SubscriptionName = companySubscription.Subscription?.Name ?? string.Empty,
                    Description = companySubscription.Subscription?.Description,
                    Price = companySubscription.Subscription?.Price ?? 0,
                    Duration = companySubscription.Subscription?.Duration,
                    ResumeLimit = companySubscription.Subscription?.ResumeLimit ?? 0,
                    HoursLimit = companySubscription.Subscription?.HoursLimit ?? 0,
                    CompareLimit = companySubscription.Subscription?.CompareLimit,
                    CompareHoursLimit = companySubscription.Subscription?.CompareHoursLimit,
                    StartDate = companySubscription.StartDate,
                    EndDate = companySubscription.EndDate,
                    SubscriptionStatus = companySubscription.SubscriptionStatus
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Current subscription retrieved successfully.",
                Data = response
            };
        }
    }
}