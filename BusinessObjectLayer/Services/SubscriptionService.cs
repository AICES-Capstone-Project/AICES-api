using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;

        public SubscriptionService(ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionRepository = subscriptionRepository;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var subscriptions = await _subscriptionRepository.GetAllAsync();

            var result = subscriptions.Select(s => new SubscriptionResponse
            {
                SubcriptionId = s.SubcriptionId,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                DurationDays = s.DurationDays,
                Limit = s.Limit,
                IsActive = s.IsActive,
                CreatedAt = (DateTime)s.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscriptions retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> GetAllByAdminAsync()
        {
            var subscriptions = await _subscriptionRepository.GetAllAsync(includeInactive: true);

            var result = subscriptions.Select(s => new SubscriptionResponse
            {
                SubcriptionId = s.SubcriptionId,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                DurationDays = s.DurationDays,
                Limit = s.Limit,
                IsActive = s.IsActive,
                CreatedAt = (DateTime)s.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "All subscriptions (including inactive) retrieved successfully.",
                Data = result
            };
        }



        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(id);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscription retrieved successfully.",
                Data = new SubscriptionResponse
                {
                    SubcriptionId = subscription.SubcriptionId,
                    Name = subscription.Name,
                    Description = subscription.Description,
                    Price = subscription.Price,
                    DurationDays = subscription.DurationDays,
                    Limit = subscription.Limit,
                    IsActive = subscription.IsActive,
                    CreatedAt = (DateTime)subscription.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> GetByIdForAdminAsync(int id)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(id, includeInactive: true);
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
                SubcriptionId = subscription.SubcriptionId,
                Name = subscription.Name,
                Description = subscription.Description,
                Price = subscription.Price,
                DurationDays = subscription.DurationDays,
                Limit = subscription.Limit,
                IsActive = subscription.IsActive,
                CreatedAt = (DateTime)subscription.CreatedAt
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
            if (await _subscriptionRepository.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Subscription name already exists."
                };
            }

            var subscription = new Subscription
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                DurationDays = request.DurationDays,
                Limit = request.Limit,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _subscriptionRepository.AddAsync(subscription);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscription created successfully."
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, SubscriptionRequest request)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(id);
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
            subscription.DurationDays = request.DurationDays;
            subscription.Limit = request.Limit ?? subscription.Limit;

            await _subscriptionRepository.UpdateAsync(subscription);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscription updated successfully."
            };
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(id);
            if (subscription == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Subscription not found."
                };
            }

            await _subscriptionRepository.SoftDeleteAsync(subscription);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Subscription deleted successfully."
            };
        }
    }
}
