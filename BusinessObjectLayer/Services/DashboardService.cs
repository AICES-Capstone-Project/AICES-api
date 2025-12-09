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

                int? resumeLimit;
                int? hoursLimit;
                DateTime? startDate;

                if (companySubscription != null)
                {
                    resumeLimit = companySubscription.Subscription?.ResumeLimit;
                    hoursLimit = companySubscription.Subscription?.HoursLimit;
                    startDate = companySubscription.StartDate;
                }
                else
                {
                    // Không có subscription active, sử dụng Free subscription
                    var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
                    var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
                    
                    if (freeSubscription != null)
                    {
                        resumeLimit = freeSubscription.ResumeLimit;
                        hoursLimit = freeSubscription.HoursLimit;
                        startDate = null; // Free subscription không có StartDate
                    }
                    else
                    {
                        resumeLimit = null;
                        hoursLimit = null;
                        startDate = null;
                    }
                }

                if (resumeLimit.HasValue && resumeLimit.Value > 0)
                {
                    var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                    var resumeCount = startDate.HasValue
                        ? await parsedResumeRepo.CountResumesSinceDateAsync(companyId, startDate.Value, hoursLimit ?? 0)
                        : await parsedResumeRepo.CountResumesInLastHoursAsync(companyId, hoursLimit ?? 0);

                    creditsRemaining = Math.Max(0, resumeLimit.Value - resumeCount);
                }
                else
                {
                    // No limit set, return a large number or -1 to indicate unlimited
                    creditsRemaining = -1; // -1 means unlimited
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

        public async Task<ServiceResponse> GetSystemOverviewAsync()
        {
            try
            {
                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();

                // Lấy lần lượt để tránh cạnh tranh DbContext
                var totalCompanies = await dashboardRepo.GetTotalCompaniesAsync();
                var totalUsers = await dashboardRepo.GetTotalUsersAsync();
                var totalJobs = await dashboardRepo.GetTotalJobsAsync();
                var totalResumes = await dashboardRepo.GetTotalResumesAsync();
                var totalCompanySubscriptions = await dashboardRepo.GetTotalCompanySubscriptionsAsync();
                var totalSubscriptions = await dashboardRepo.GetTotalSubscriptionsAsync();
                var totalRevenue = await dashboardRepo.GetTotalRevenueAsync();

                var response = new SystemOverviewResponse
                {
                    TotalCompanies = totalCompanies,
                    TotalUsers = totalUsers,
                    TotalJobs = totalJobs,
                    TotalResumes = totalResumes,
                    TotalCompanySubscriptions = totalCompanySubscriptions,
                    TotalSubscriptions = totalSubscriptions,
                    TotalRevenue = totalRevenue
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "System overview retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get system overview error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving system overview."
                };
            }
        }

        public async Task<ServiceResponse> GetSystemCompanyStatsAsync()
        {
            try
            {
                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();

                var totalCompanies = await dashboardRepo.GetTotalCompaniesAsync();
                var approvedCompanies = await dashboardRepo.GetTotalCompaniesByStatusAsync(CompanyStatusEnum.Approved);
                var pendingCompanies = await dashboardRepo.GetTotalCompaniesByStatusAsync(CompanyStatusEnum.Pending);
                var rejectedCompanies = await dashboardRepo.GetTotalCompaniesByStatusAsync(CompanyStatusEnum.Rejected);
                var suspendedCompanies = await dashboardRepo.GetTotalCompaniesByStatusAsync(CompanyStatusEnum.Suspended);
                var newCompaniesThisMonth = await dashboardRepo.GetNewCompaniesThisMonthAsync();

                var response = new SystemCompanyStatsResponse
                {
                    TotalCompanies = totalCompanies,
                    ApprovedCompanies = approvedCompanies,
                    PendingCompanies = pendingCompanies,
                    RejectedCompanies = rejectedCompanies,
                    SuspendedCompanies = suspendedCompanies,
                    NewCompaniesThisMonth = newCompaniesThisMonth
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company statistics retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get system company stats error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving company statistics."
                };
            }
        }

        public async Task<ServiceResponse> GetSystemTopCompaniesAsync(int top = 10)
        {
            try
            {
                var dashboardRepo = _uow.GetRepository<IDashboardRepository>();
                var topCompanies = await dashboardRepo.GetTopCompaniesByResumeAndJobAsync(top);

                var response = topCompanies.Select(x => new TopCompanyDashboardResponse
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.CompanyName,
                    ResumeCount = x.ResumeCount,
                    JobCount = x.JobCount
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Top companies retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get system top companies error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving top companies."
                };
            }
        }
    }
}

