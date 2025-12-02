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
    }
}

