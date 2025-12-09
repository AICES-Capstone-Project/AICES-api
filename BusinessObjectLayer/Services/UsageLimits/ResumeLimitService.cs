using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services.UsageLimits
{
    public class ResumeLimitService : IResumeLimitService
    {
        private readonly IUnitOfWork _uow;

        public ResumeLimitService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> CheckResumeLimitAsync(int companyId)
        {
            try
            {
                // Get active subscription for company
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);

                int? resumeLimit;
                int? hoursLimit;
                DateTime? startDate;

                if (companySubscription == null)
                {
                    // Không có subscription active, sử dụng Free subscription
                    var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                    var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
                    
                    if (freeSubscription == null)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.NotFound,
                            Message = "No active subscription found and Free subscription not configured."
                        };
                    }

                    resumeLimit = freeSubscription.ResumeLimit;
                    hoursLimit = freeSubscription.HoursLimit;
                    startDate = null; // Free subscription không có StartDate
                }
                else
                {
                    // Get resume limit and hours limit from subscription
                    resumeLimit = companySubscription.Subscription?.ResumeLimit;
                    hoursLimit = companySubscription.Subscription?.HoursLimit;
                    startDate = companySubscription.StartDate;
                }

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed."
                    };
                }

                // Count resumes uploaded since subscription start date or in last HoursLimit hours for Free plan
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                var resumeCount = startDate.HasValue
                    ? await parsedResumeRepo.CountResumesSinceDateAsync(companyId, startDate.Value, hoursLimit ?? 0)
                    : await parsedResumeRepo.CountResumesInLastHoursAsync(companyId, hoursLimit ?? 0);

                if (resumeCount >= resumeLimit)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Resume upload limit reached. You have uploaded {resumeCount} resumes in the last {hoursLimit} hours. Your subscription allows {resumeLimit} resumes per {hoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = resumeCount,
                            Limit = resumeLimit,
                            HoursLimit = hoursLimit,
                            Remaining = 0
                        }
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume limit check passed.",
                    Data = new
                    {
                        CurrentCount = resumeCount,
                        Limit = resumeLimit,
                        HoursLimit = hoursLimit,
                        Remaining = resumeLimit - resumeCount
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking resume limit: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> CheckResumeLimitInTransactionAsync(int companyId)
        {
            try
            {
                // Get active subscription for company WITH LOCK to prevent race conditions
                // This ensures only one transaction can check limit at a time for the same company
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var companySubscription = await companySubRepo.GetAnyActiveSubscriptionForUpdateByCompanyAsync(companyId);

                int? resumeLimit;
                int? hoursLimit;
                DateTime? startDate;

                if (companySubscription == null)
                {
                    // Không có subscription active, sử dụng Free subscription (không cần lock vì không có CompanySubscription)
                    var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                    var allSubscriptions = await subscriptionRepo.GetAllAsync();
                    var freeSubscription = allSubscriptions.FirstOrDefault(s => 
                        s.Price == 0 || s.Name.Equals("Free", StringComparison.OrdinalIgnoreCase));
                    
                    if (freeSubscription == null)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.NotFound,
                            Message = "No active subscription found and Free subscription not configured."
                        };
                    }

                    resumeLimit = freeSubscription.ResumeLimit;
                    hoursLimit = freeSubscription.HoursLimit;
                    startDate = null; // Free subscription không có StartDate
                }
                else
                {
                    // Get resume limit and hours limit from subscription
                    resumeLimit = companySubscription.Subscription?.ResumeLimit;
                    hoursLimit = companySubscription.Subscription?.HoursLimit;
                    startDate = companySubscription.StartDate;
                }

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed."
                    };
                }

                // Count resumes uploaded since subscription start date or in last HoursLimit hours for Free plan
                // Use InTransaction method to see records created in current transaction
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                var resumeCount = startDate.HasValue
                    ? await parsedResumeRepo.CountResumesSinceDateInTransactionAsync(companyId, startDate.Value, hoursLimit ?? 0)
                    : await parsedResumeRepo.CountResumesInLastHoursInTransactionAsync(companyId, hoursLimit ?? 0);

                if (resumeCount >= resumeLimit)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Resume upload limit reached. You have uploaded {resumeCount} resumes in the last {hoursLimit} hours. Your subscription allows {resumeLimit} resumes per {hoursLimit} hours.",
                        Data = new
                        {
                            CurrentCount = resumeCount,
                            Limit = resumeLimit,
                            HoursLimit = hoursLimit,
                            Remaining = 0
                        }
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume limit check passed.",
                    Data = new
                    {
                        CurrentCount = resumeCount,
                        Limit = resumeLimit,
                        HoursLimit = hoursLimit,
                        Remaining = resumeLimit - resumeCount
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Error checking resume limit: {ex.Message}"
                };
            }
        }
    }
}

