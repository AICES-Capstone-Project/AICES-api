using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IUnitOfWork _uow;

        public SubscriptionService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllByAdminAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscriptions = await subscriptionRepo.GetSubscriptionsAsync(page, pageSize, search);
            var total = await subscriptionRepo.GetTotalSubscriptionsAsync(search);

            var pagedData = subscriptions.Select(s => new SubscriptionResponse
            {
                SubscriptionId = s.SubscriptionId,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                Duration = s.Duration,
                ResumeLimit = s.ResumeLimit,
                HoursLimit = s.HoursLimit,
                CreatedAt = s.CreatedAt
            }).ToList();

            var responseData = new
            {
                Subscriptions = pagedData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscriptions retrieved successfully.",
                Data = responseData
            };
        }

        public async Task<ServiceResponse> GetByIdForAdminAsync(int id)
        {
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscription = await subscriptionRepo.GetByIdAsync(id);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            var result = new SubscriptionResponse
            {
                SubscriptionId = subscription.SubscriptionId,
                Name = subscription.Name,
                Description = subscription.Description,
                Price = subscription.Price,
                Duration = subscription.Duration,
                ResumeLimit = subscription.ResumeLimit,
                HoursLimit = subscription.HoursLimit,
                CreatedAt = subscription.CreatedAt
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscription (admin view) retrieved successfully.",
                Data = result
            };
        }


        public async Task<ServiceResponse> CreateAsync(SubscriptionRequest request)
        {
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            
            if (await subscriptionRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Subscription name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var subscription = new Subscription
                {
                    Name = request.Name,
                    Description = request.Description,
                    Price = request.Price,
                    Duration = request.Duration,
                    ResumeLimit = request.ResumeLimit,
                    HoursLimit = request.HoursLimit,
                    StripePriceId = request.StripePriceId,
                };

                await subscriptionRepo.AddAsync(subscription);
                await _uow.SaveChangesAsync();
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, SubscriptionRequest request)
        {
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscription = await subscriptionRepo.GetForUpdateAsync(id);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            subscription.Name = request.Name ?? subscription.Name;
            subscription.Description = request.Description ?? subscription.Description;
            subscription.Price = request.Price;
            subscription.Duration = request.Duration;
            subscription.ResumeLimit = request.ResumeLimit;
            subscription.HoursLimit = request.HoursLimit;
            subscription.StripePriceId = request.StripePriceId;
            
            await _uow.BeginTransactionAsync();
            try
            {
                await subscriptionRepo.UpdateAsync(subscription);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription updated successfully."
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
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscription = await subscriptionRepo.GetForUpdateAsync(id);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            // Check if subscription has any associated CompanySubscriptions
            var companySubscriptionRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var hasCompanySubscriptions = await companySubscriptionRepo.HasAnyBySubscriptionIdAsync(id);
            if (hasCompanySubscriptions)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Cannot delete subscription that has associated company subscriptions."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                await subscriptionRepo.SoftDeleteAsync(subscription);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription deleted successfully."
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
