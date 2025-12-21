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

        public ResumeApplicationService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
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
    }
}
