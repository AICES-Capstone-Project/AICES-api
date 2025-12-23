using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

namespace BusinessObjectLayer.Services
{
    public class ResumeApplicationService : IResumeApplicationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICampaignRepository _campaignRepo;

        public ResumeApplicationService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _campaignRepo = _uow.GetRepository<ICampaignRepository>();
        }

        private async Task<(ServiceResponse? errorResponse, int? companyId)> GetCurrentUserCompanyIdAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                }, null);
            }

            int userId = int.Parse(userIdClaim);

            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company user not found."
                }, null);
            }

            if (companyUser.CompanyId == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "You are not associated with any company."
                }, null);
            }

            if (companyUser.JoinStatus != JoinStatusEnum.Approved)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "You must be approved to access company data."
                }, null);
            }

            return (null, companyUser.CompanyId.Value);
        }

        public async Task<ServiceResponse> UpdateAdjustedScoreAsync(int applicationId, UpdateAdjustedScoreRequest request, ClaimsPrincipal user)
        {
            try
            {
                // Get current user ID
                var userClaims = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = userClaims != null ? ClaimUtils.GetUserIdClaim(userClaims) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get user with RoleId
                var userRepo = _uow.GetRepository<IUserRepository>();
                var currentUser = await userRepo.GetByIdAsync(userId);
                if (currentUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "User not found."
                    };
                }

                int roleId = currentUser.RoleId;

                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();
                var application = await resumeApplicationRepo.GetApplicationByIdAndCompanyAsync(applicationId, companyId);

                if (application == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found."
                    };
                }

                // Get campaign to check status
                if (application.CampaignId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Application is not associated with a campaign."
                    };
                }

                var campaign = await _campaignRepo.GetByIdAsync(application.CampaignId.Value);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Associated campaign not found."
                    };
                }

                if (campaign.Status != CampaignStatusEnum.Published)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Cannot adjust score for an application in a non-Published campaign."
                    };
                }

                // Business logic for score adjustment
                // HR Recruiter (roleId = 5): can only adjust once, after that isAdjusted = true
                // HR Manager (roleId = 4): can adjust multiple times, isAdjusted always set to true at first time, after that only HR Manager can modify
                
                if (roleId != 4 && roleId != 5)
                {
                    // Other roles are not allowed to adjust scores
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only HR Recruiter and HR Manager can adjust scores."
                    };
                }

                if (roleId == 5) // HR Recruiter
                {
                    // Check if already adjusted (HR Recruiter can only adjust once)
                    if (application.IsAdjusted)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Forbidden,
                            Message = "HR Recruiter can only adjust the score once. The score has already been adjusted."
                        };
                    }
                    
                    // First time adjustment by HR Recruiter
                    application.AdjustedScore = request.AdjustedScore;
                    application.IsAdjusted = true;
                    application.AdjustedBy = userId;
                }
                else if (roleId == 4) // HR Manager
                {
                    // HR Manager can always adjust
                    // If this is the first time any adjustment is made, set isAdjusted = true
                    if (!application.IsAdjusted)
                    {
                        application.IsAdjusted = true;
                    }
                    
                    // Update the score and track who adjusted it
                    application.AdjustedScore = request.AdjustedScore;
                    application.AdjustedBy = userId;
                }

                await resumeApplicationRepo.UpdateAsync(application);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Adjusted score updated successfully.",
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the adjusted score.",
                    Data = ex.Message
                };
            }
        }

        public async Task<ServiceResponse> UpdateStatusAsync(int applicationId, UpdateApplicationStatusRequest request, ClaimsPrincipal user)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();
                var application = await resumeApplicationRepo.GetApplicationByIdAndCompanyAsync(applicationId, companyId);

                if (application == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found."
                    };
                }

                // Get campaign to check status
                if (application.CampaignId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Application is not associated with a campaign."
                    };
                }

                var campaign = await _campaignRepo.GetByIdAsync(application.CampaignId.Value);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Associated campaign not found."
                    };
                }

                if (campaign.Status != CampaignStatusEnum.Published)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Cannot update status for an application in a non-Published campaign."
                    };
                }

                var currentStatus = application.Status;
                var newStatus = request.Status;

                // Validate status transition: reviewed -> shortlisted -> interview -> rejected / hired
                bool isValidTransition = false;

                if (currentStatus == ApplicationStatusEnum.Reviewed && newStatus == ApplicationStatusEnum.Shortlisted)
                {
                    isValidTransition = true;
                }
                else if (currentStatus == ApplicationStatusEnum.Shortlisted && newStatus == ApplicationStatusEnum.Interview)
                {
                    isValidTransition = true;
                }
                else if (currentStatus == ApplicationStatusEnum.Interview && (newStatus == ApplicationStatusEnum.Rejected || newStatus == ApplicationStatusEnum.Hired))
                {
                    isValidTransition = true;
                }

                if (!isValidTransition)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Invalid status transition. Current status: {currentStatus}. " +
                                 "Allowed transitions: Reviewed -> Shortlisted -> Interview -> Rejected/Hired"
                    };
                }

                // Update status
                application.Status = newStatus;
                
                // Update note if provided, otherwise keep existing value
                if (request.Note != null)
                {
                    application.Note = request.Note;
                }
                
                await resumeApplicationRepo.UpdateAsync(application);
                await _uow.SaveChangesAsync();

                // Update CurrentHired in JobCampaign if status changed to/from Hired and application has CampaignId
                if (application.CampaignId.HasValue && 
                    (currentStatus == ApplicationStatusEnum.Hired || newStatus == ApplicationStatusEnum.Hired))
                {
                    var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                    await campaignRepo.UpdateJobCampaignCurrentHiredAsync(application.JobId, application.CampaignId.Value);
                    await _uow.SaveChangesAsync();
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Application status updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the application status.",
                    Data = ex.Message
                };
            }
        }

        /// <summary>
        /// Get list of resumes for a specific job in a campaign with pagination and filtering
        /// </summary>
        public async Task<ServiceResponse> GetJobResumesAsync(int jobId, int campaignId, GetJobResumesRequest request)
        {
            try
            {
                if (request.Page <= 0 || request.PageSize <= 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Page and pageSize must be greater than zero."
                    };
                }

                var (errorResponse, companyId) = await GetCurrentUserCompanyIdAsync();
                if (errorResponse != null) return errorResponse;

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();

                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view resumes for this job."
                    };
                }

                var (resumeApplications, totalCount) = await resumeApplicationRepo.GetByJobIdAndCampaignWithResumePagedAsync(
                    jobId, campaignId, request.Page, request.PageSize, 
                    request.Search, request.MinScore, request.MaxScore, request.ApplicationStatus);

                var resumeList = resumeApplications
                    .Select(application => new JobResumeListResponse
                    {
                        ResumeId = application.ResumeId,
                        ApplicationId = application.ApplicationId,
                        ResumeStatus = application.Resume?.Status,
                        ApplicationStatus = application.Status,
                        ApplicationErrorType = application.ErrorType,
                        FullName = application.Resume?.Candidate?.FullName ?? "Unknown",
                        TotalScore = application.TotalScore,
                        AdjustedScore = application.AdjustedScore,
                        Note = application.Note
                    })
                    .ToList();

                var paginatedResponse = new PaginatedJobResumeListResponse
                {
                    Resumes = resumeList,
                    TotalCount = totalCount,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job resumes retrieved successfully.",
                    Data = paginatedResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting job resumes: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving job resumes."
                };
            }
        }

        /// <summary>
        /// Get detailed information about a specific resume application
        /// </summary>
        public async Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int applicationId, int campaignId)
        {
            try
            {
                var (errorResponse, companyId) = await GetCurrentUserCompanyIdAsync();
                if (errorResponse != null) return errorResponse;

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();

                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this resume."
                    };
                }

                var resumeApplication = await resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(applicationId);

                if (resumeApplication == null || resumeApplication.JobId != jobId || resumeApplication.CampaignId != campaignId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found for this job and campaign."
                    };
                }

                var resume = resumeApplication.Resume;
                var candidate = resume?.Candidate;
                
                var scoreDetailsResponse = resumeApplication?.ScoreDetails?
                    .Select(detail => new ResumeScoreDetailResponse
                    {
                        CriteriaId = detail.CriteriaId,
                        CriteriaName = detail.Criteria?.Name ?? "",
                        Matched = detail.Matched,
                        Score = detail.Score,
                        AINote = detail.AINote
                    }).ToList() ?? new List<ResumeScoreDetailResponse>();

                var response = new JobResumeDetailResponse
                {
                    ResumeId = resume?.ResumeId,
                    ApplicationId = resumeApplication?.ApplicationId,
                    QueueJobId = resumeApplication?.QueueJobId ?? string.Empty,
                    FileUrl = resume?.FileUrl ?? string.Empty,
                    OriginalFileName = resume?.OriginalFileName,
                    ResumeStatus = resume?.Status,
                    ApplicationStatus = resumeApplication?.Status,
                    ApplicationErrorType = resumeApplication?.ErrorType,
                    CampaignId = resumeApplication?.CampaignId,
                    CreatedAt = resume?.CreatedAt,
                    CandidateId = resumeApplication?.CandidateId ?? candidate?.CandidateId ?? 0,
                    FullName = candidate?.FullName ?? "Unknown",
                    Email = candidate?.Email ?? "N/A",
                    PhoneNumber = candidate?.PhoneNumber,
                    MatchSkills = resumeApplication?.MatchSkills,
                    MissingSkills = resumeApplication?.MissingSkills,
                    TotalScore = resumeApplication?.TotalScore,
                    AdjustedScore = resumeApplication?.AdjustedScore,
                    AIExplanation = resumeApplication?.AIExplanation,
                    ErrorMessage = resumeApplication?.ErrorMessage,
                    Note = resumeApplication?.Note,
                    ScoreDetails = scoreDetailsResponse
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume detail retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting job resume detail: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving resume detail."
                };
            }
        }
    }
}
