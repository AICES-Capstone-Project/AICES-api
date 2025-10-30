using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanyUserService : ICompanyUserService
    {
        private readonly ICompanyUserRepository _companyUserRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IUserRepository _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CompanyUserService(
            ICompanyUserRepository companyUserRepository,
            ICompanyRepository companyRepository,
            IUserRepository userRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _companyUserRepository = companyUserRepository;
            _companyRepository = companyRepository;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId)
        {
            try
            {
                // Create CompanyUser with null CompanyId (user chưa join company nào)
                var companyUser = new CompanyUser
                {
                    UserId = userId,
                    JoinStatus = JoinStatusEnum.NotApplied,
                    IsActive = true
                };
                
                await _companyUserRepository.AddCompanyUserAsync(companyUser);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Default company user created successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating company user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Failed to create default company user."
                };
            }
        }

        public async Task<ServiceResponse> GetMembersByCompanyIdAsync(int companyId)
        {
            try
            {
                var members = await _companyUserRepository.GetMembersByCompanyIdAsync(companyId);

                var responses = members.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    JoinStatus = m.JoinStatus,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company members retrieved successfully.",
                    Data = responses
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get company members error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving company members."
                };
            }
        }

        public async Task<ServiceResponse> SendJoinRequestAsync(int companyId)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
                }
                int userId = int.Parse(userIdClaim);

                var company = await _companyRepository.GetPublicByIdAsync(companyId);
                if (company == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company not found or not approved." };
                }

                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    companyUser = new CompanyUser { UserId = userId, IsActive = true };
                    await _companyUserRepository.AddCompanyUserAsync(companyUser);
                }

                if (companyUser.CompanyId != null)
                {
                    if (companyUser.CompanyId == companyId && companyUser.JoinStatus == JoinStatusEnum.Pending)
                    {
                        return new ServiceResponse { Status = SRStatus.Duplicated, Message = "You already have a pending request for this company." };
                    }
                    if (companyUser.JoinStatus == JoinStatusEnum.Approved)
                    {
                        return new ServiceResponse { Status = SRStatus.Duplicated, Message = "You already belong to a company." };
                    }
                }

                companyUser.CompanyId = companyId;
                companyUser.JoinStatus = JoinStatusEnum.Pending;
                await _companyUserRepository.UpdateAsync(companyUser);

                return new ServiceResponse { Status = SRStatus.Success, Message = "Join request sent." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send join request error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while sending join request." };
            }
        }

        public async Task<ServiceResponse> GetPendingJoinRequestsAsync(int companyId)
        {
            try
            {
                var auth = await CheckManagerOfCompany(companyId);
                if (auth.Status != SRStatus.Success) return auth;

                var pendings = await _companyUserRepository.GetPendingByCompanyIdAsync(companyId);
                var data = pendings.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    JoinStatus = m.JoinStatus,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt
                }).ToList();

                return new ServiceResponse { Status = SRStatus.Success, Message = "Pending requests retrieved.", Data = data };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get pending requests error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while retrieving pending requests." };
            }
        }

        public async Task<ServiceResponse> UpdateJoinRequestStatusAsync(int companyId, int comUserId, JoinStatusEnum joinStatus)
        {
            try
            {
                var auth = await CheckManagerOfCompany(companyId);
                if (auth.Status != SRStatus.Success) return auth;

                var companyUser = await _companyUserRepository.GetByComUserIdAsync(comUserId);
                if (companyUser == null || companyUser.CompanyId != companyId)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Join request not found for this company." };
                }

                if (joinStatus != JoinStatusEnum.Approved && joinStatus != JoinStatusEnum.Rejected)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "Invalid status. Use Approved or Rejected." };
                }

                companyUser.JoinStatus = joinStatus;
                // Note: rejectionReason not stored due to schema lacking a field.
                await _companyUserRepository.UpdateAsync(companyUser);

                return new ServiceResponse { Status = SRStatus.Success, Message = "Join request updated." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update join request status error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while updating join request." };
            }
        }

        public async Task<ServiceResponse> GetSelfCompanyMembersAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
            if (string.IsNullOrEmpty(userIdClaim))
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            int userId = int.Parse(userIdClaim);
            var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
            {
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "You are not associated with any company." };
            }
            return await GetMembersByCompanyIdAsync(companyUser.CompanyId.Value);
        }

        public async Task<ServiceResponse> GetPendingJoinRequestsSelfAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
            if (string.IsNullOrEmpty(userIdClaim))
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            int userId = int.Parse(userIdClaim);
            var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not a manager of any company." };
            return await GetPendingJoinRequestsAsync(companyUser.CompanyId.Value);
        }

        public async Task<ServiceResponse> UpdateJoinRequestStatusSelfAsync(int comUserId, JoinStatusEnum joinStatus)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
            if (string.IsNullOrEmpty(userIdClaim))
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            int userId = int.Parse(userIdClaim);
            var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not a manager of any company." };
            return await UpdateJoinRequestStatusAsync(companyUser.CompanyId.Value, comUserId, joinStatus);
        }

        private async Task<ServiceResponse> CheckManagerOfCompany(int companyId)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            }
            int userId = int.Parse(userIdClaim);
            var userEntity = await _userRepository.GetByIdAsync(userId);
            if (userEntity == null || userEntity.Role.RoleName != "HR_Manager")
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "Only HR_Manager can perform this action." };
            }
            var managerCompanyUser = await _companyUserRepository.GetByUserIdAsync(userId);
            if (managerCompanyUser?.CompanyId != companyId)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not a manager of this company." };
            }
            return new ServiceResponse { Status = SRStatus.Success };
        }
    }
}
