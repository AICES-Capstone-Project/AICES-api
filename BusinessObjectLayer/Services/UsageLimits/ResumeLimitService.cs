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

                if (companySubscription == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No active subscription found for your company."
                    };
                }

                // Get resume limit and hours limit from subscription
                int? resumeLimit = companySubscription.Subscription?.ResumeLimit;
                int? hoursLimit = companySubscription.Subscription?.HoursLimit;

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed."
                    };
                }

                // Count resumes uploaded in the last X hours (based on HoursLimit) for this company
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                var resumeCount = await parsedResumeRepo.CountResumesInLastHoursAsync(companyId, hoursLimit ?? 0);

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
                // Get active subscription for company
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);

                if (companySubscription == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No active subscription found for your company."
                    };
                }

                // Get resume limit and hours limit from subscription
                int? resumeLimit = companySubscription.Subscription?.ResumeLimit;
                int? hoursLimit = companySubscription.Subscription?.HoursLimit;

                if (resumeLimit <= 0)
                {
                    // No limit set, allow upload
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume limit check passed."
                    };
                }

                // Count resumes uploaded in the last X hours (based on HoursLimit) for this company
                // Use InTransaction method to see records created in current transaction
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                var resumeCount = await parsedResumeRepo.CountResumesInLastHoursInTransactionAsync(companyId, hoursLimit ?? 0);

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

