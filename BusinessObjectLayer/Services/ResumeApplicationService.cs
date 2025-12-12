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

                // Update the adjusted score
                application.AdjustedScore = request.AdjustedScore;

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
    }
}
