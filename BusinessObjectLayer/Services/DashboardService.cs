using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardService(
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse> GetTopCategorySpecByResumeCountAsync(int top = 10)
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get company user to find associated company
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You are not associated with any company."
                    };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access company dashboard."
                    };
                }

                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();
                var topCategorySpecs = await dashboardRepo.GetTopCategorySpecByResumeCountAsync(companyUser.CompanyId.Value, top);

                var response = topCategorySpecs.Select(x => new TopCategorySpecResponse
                {
                    CategoryId = x.CategoryId,
                    CategoryName = x.CategoryName,
                    SpecializationId = x.SpecializationId,
                    SpecializationName = x.SpecializationName,
                    ResumeCount = x.ResumeCount
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Top category-specialization retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get top category-spec error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving top category-specialization."
                };
            }
        }

        public async Task<ServiceResponse> GetDashboardSummaryAsync()
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get company user to find associated company
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You are not associated with any company."
                    };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access company dashboard."
                    };
                }

                var companyId = companyUser.CompanyId.Value;
                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();

                // Get all metrics sequentially to avoid DbContext concurrency issues
                var activeJobs = await dashboardRepo.GetActiveJobsCountAsync(companyId);
                var totalMembers = await dashboardRepo.GetTotalMembersCountAsync(companyId);
                var aiProcessed = await dashboardRepo.GetAiProcessedCountAsync(companyId);

                // Calculate credits remaining
                int creditsRemaining = 0;
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);

                if (companySubscription != null)
                {
                    int? resumeLimit = companySubscription.Subscription?.ResumeLimit;
                    int? hoursLimit = companySubscription.Subscription?.HoursLimit;

                    if (resumeLimit.HasValue && resumeLimit.Value > 0)
                    {
                        var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                        var resumeCount = await parsedResumeRepo.CountResumesSinceDateAsync(
                            companyId,
                            companySubscription.StartDate,
                            hoursLimit ?? 0);

                        creditsRemaining = Math.Max(0, resumeLimit.Value - resumeCount);
                    }
                    else
                    {
                        // No limit set, return a large number or -1 to indicate unlimited
                        creditsRemaining = -1; // -1 means unlimited
                    }
                }

                var response = new DashboardSummaryResponse
                {
                    ActiveJobs = activeJobs,
                    TotalMembers = totalMembers,
                    AiProcessed = aiProcessed,
                    CreditsRemaining = creditsRemaining
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Dashboard summary retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get dashboard summary error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving dashboard summary."
                };
            }
        }

        public async Task<ServiceResponse> GetTopRatedCandidatesAsync(int limit = 5)
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get company user to find associated company
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You are not associated with any company."
                    };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access company candidates."
                    };
                }

                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();
                var topCandidates = await dashboardRepo.GetTopRatedCandidatesAsync(companyUser.CompanyId.Value, limit);

                var response = topCandidates.Select(x => new TopRatedCandidateResponse
                {
                    Name = x.Name,
                    JobTitle = x.JobTitle,
                    AIScore = x.AIScore,
                    Status = x.Status
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Top rated candidates retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get top rated candidates error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving top rated candidates."
                };
            }
        }
    }
}

