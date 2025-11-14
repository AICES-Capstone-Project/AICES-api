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

        public async Task<ServiceResponse> GetAllByAdminAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var subscriptions = await _subscriptionRepository.GetSubscriptionsAsync(page, pageSize, search);
            var total = await _subscriptionRepository.GetTotalSubscriptionsAsync(search);

            var pagedData = subscriptions.Select(s => new SubscriptionResponse
            {
                SubscriptionId = s.SubscriptionId,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                DurationDays = s.DurationDays,
                Limit = s.Limit,
                CreatedAt = (DateTime)s.CreatedAt
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
            var subscription = await _subscriptionRepository.GetByIdAsync(id);
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
                DurationDays = subscription.DurationDays,
                Limit = subscription.Limit,
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
