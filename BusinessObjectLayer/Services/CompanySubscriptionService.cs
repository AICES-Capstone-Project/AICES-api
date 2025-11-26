using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Data.Enum;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanySubscriptionService : ICompanySubscriptionService
    {
        private readonly IUnitOfWork _uow;

        public CompanySubscriptionService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscriptions = await companySubscriptionRepo.GetCompanySubscriptionsAsync(page, pageSize, search);
            var total = await companySubscriptionRepo.GetTotalCompanySubscriptionsAsync(search);

            var pagedData = companySubscriptions.Select(cs => new CompanySubscriptionResponse
            {
                ComSubId = cs.ComSubId,
                CompanyId = cs.CompanyId,
                CompanyName = cs.Company?.Name ?? string.Empty,
                SubscriptionId = cs.SubscriptionId,
                SubscriptionName = cs.Subscription?.Name ?? string.Empty,
                StartDate = cs.StartDate,
                EndDate = cs.EndDate,
                SubscriptionStatus = cs.SubscriptionStatus,
                CreatedAt = cs.CreatedAt
            }).ToList();

            var responseData = new
            {
                CompanySubscriptions = pagedData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company subscriptions retrieved successfully.",
                Data = responseData
            };
        }

        public async Task<ServiceResponse> CreateAsync(CreateCompanySubscriptionRequest request)
        {
            var now = DateTime.UtcNow;

            if (request.StartDate < now)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Start date cannot be in the past."
                };
            }

            var companyRepo = _uow.GetRepository<ICompanyRepository>();
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            
            var company = await companyRepo.GetByIdAsync(request.CompanyId);
            if (company == null || !company.IsActive)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            var subscription = await subscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            var endDate = request.StartDate.AddDays(subscription.DurationDays);

            var anyActiveSubscription = await companySubscriptionRepo.GetAnyActiveSubscriptionByCompanyAsync(request.CompanyId);
            if (anyActiveSubscription != null && anyActiveSubscription.SubscriptionId != request.SubscriptionId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Company already has an active or pending subscription. Cannot create another subscription until the current one is canceled, expired, or inactive."
                };
            }

            var activeSubscription = await companySubscriptionRepo.GetActiveSubscriptionAsync(request.CompanyId, request.SubscriptionId);

            if (activeSubscription != null)
            {
                if (!request.Renew)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "Active subscription already exists."
                    };
                }

                var newStartDate = activeSubscription.EndDate.AddSeconds(1);
                var newEndDate = newStartDate.AddDays(subscription.DurationDays);

                var renewedSubscription = new CompanySubscription
                {
                    CompanyId = request.CompanyId,
                    SubscriptionId = request.SubscriptionId,
                    StartDate = newStartDate,
                    EndDate = newEndDate,
                    SubscriptionStatus = SubscriptionStatusEnum.Pending,
                    IsActive = false
                };

                await _uow.BeginTransactionAsync();
                try
                {
                    await companySubscriptionRepo.AddAsync(renewedSubscription);
                    await _uow.SaveChangesAsync();
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Renewal subscription created successfully.",
                    };
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }

            var requestedStatus = request.Status ?? SubscriptionStatusEnum.Pending;

            if (requestedStatus == SubscriptionStatusEnum.Active)
            {
                var overlapping = await companySubscriptionRepo.GetActiveSubscriptionAsync(request.CompanyId, request.SubscriptionId);
                if (overlapping != null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Cannot create active subscription: overlapping subscription exists."
                    };
                }
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var companySubscription = new CompanySubscription
                {
                    CompanyId = request.CompanyId,
                    SubscriptionId = request.SubscriptionId,
                    StartDate = request.StartDate,
                    EndDate = endDate,
                    SubscriptionStatus = requestedStatus
                };

                await companySubscriptionRepo.AddAsync(companySubscription);
                await _uow.SaveChangesAsync();
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company subscription created successfully.",
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscription = await companySubscriptionRepo.GetByIdAsync(id);
            if (companySubscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company subscription not found."
                };
            }

            var result = new CompanySubscriptionResponse
            {
                ComSubId = companySubscription.ComSubId,
                CompanyId = companySubscription.CompanyId,
                CompanyName = companySubscription.Company?.Name ?? string.Empty,
                SubscriptionId = companySubscription.SubscriptionId,
                SubscriptionName = companySubscription.Subscription?.Name ?? string.Empty,
                StartDate = companySubscription.StartDate,
                EndDate = companySubscription.EndDate,
                SubscriptionStatus = companySubscription.SubscriptionStatus,
                CreatedAt = companySubscription.CreatedAt
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company subscription retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, CompanySubscriptionRequest request)
        {
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companyRepo = _uow.GetRepository<ICompanyRepository>();
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            
            var companySubscription = await companySubscriptionRepo.GetByIdAsync(id);
            if (companySubscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company subscription not found."
                };
            }

            // Validate company exists
            var company = await companyRepo.GetByIdAsync(request.CompanyId);
            if (company == null || !company.IsActive)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found or inactive."
                };
            }

            // Validate subscription exists
            var subscription = await subscriptionRepo.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            // Update fields from CompanySubscriptionRequest
            companySubscription.CompanyId = request.CompanyId;
            companySubscription.SubscriptionId = request.SubscriptionId;
            
            if (request.StartDate.HasValue)
                companySubscription.StartDate = request.StartDate.Value;
            
            if (request.EndDate.HasValue)
                companySubscription.EndDate = request.EndDate.Value;

            await _uow.BeginTransactionAsync();
            try
            {
                await companySubscriptionRepo.UpdateAsync(companySubscription);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company subscription updated successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscription = await companySubscriptionRepo.GetForUpdateAsync(id);
            if (companySubscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company subscription not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                await companySubscriptionRepo.SoftDeleteAsync(companySubscription);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company subscription deleted successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }
    }
}

