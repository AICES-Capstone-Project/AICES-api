using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
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
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationService _notificationService;

        public CompanyUserService(
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor,
            INotificationService notificationService)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _notificationService = notificationService;
        }

        public async Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId)
        {
            try
            {
                // Create CompanyUser with null CompanyId (user chưa join company nào)
                var companyUser = new CompanyUser
                {
                    UserId = userId,
                    JoinStatus = JoinStatusEnum.NotApplied
                };
                
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                await companyUserRepo.AddCompanyUserAsync(companyUser);
                await _uow.SaveChangesAsync();

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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var members = await companyUserRepo.GetMembersByCompanyIdAsync(companyId);

                var responses = members.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    Address = m.User?.Profile?.Address,
                    JoinStatus = m.JoinStatus,
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

                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                
                var company = await companyRepo.GetPublicByIdAsync(companyId);
                if (company == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company not found or not approved." };
                }

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        companyUser = new CompanyUser { UserId = userId };
                        await companyUserRepo.AddCompanyUserAsync(companyUser);
                        await _uow.SaveChangesAsync();
                        await _uow.CommitTransactionAsync();
                    }
                    catch
                    {
                        await _uow.RollbackTransactionAsync();
                        throw;
                    }
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

                await _uow.BeginTransactionAsync();
                try
                {
                    companyUser.CompanyId = companyId;
                    companyUser.JoinStatus = JoinStatusEnum.Pending;
                    await companyUserRepo.UpdateAsync(companyUser);
                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                // After the join request is successfully marked as pending,
                // notify all managers (roleId = 4) of this company.
                try
                {
                    var members = await companyUserRepo.GetMembersByCompanyIdAsync(companyId);
                    var managers = members
                        .Where(m => m.User != null && m.User.RoleId == 4)
                        .ToList();

                    if (managers.Any())
                    {
                        var userRepo = _uow.GetRepository<IUserRepository>();
                        var recruiterUser = await userRepo.GetByIdAsync(userId);

                        var recruiterName = recruiterUser?.Profile?.FullName
                            ?? recruiterUser?.Email
                            ?? "A recruiter";

                        foreach (var manager in managers)
                        {
                            await _notificationService.CreateAsync(
                                userId: manager.UserId,
                                type: NotificationTypeEnum.Member,
                                message: $"New join request from {recruiterName}",
                                detail: $"Recruiter {recruiterName} has requested to join your company '{company.Name}'."
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but do not fail the join request if notification fails
                    Console.WriteLine($"Error sending join request notification to managers: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }

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

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var pendings = await companyUserRepo.GetPendingByCompanyIdAsync(companyId);
                var data = pendings.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    Address = m.User?.Profile?.Address,
                    JoinStatus = m.JoinStatus,
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

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByComUserIdAsync(comUserId);
                if (companyUser == null || companyUser.CompanyId != companyId)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Join request not found for this company." };
                }

                if (joinStatus != JoinStatusEnum.Approved && joinStatus != JoinStatusEnum.NotApplied)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "Invalid status. Only allows Approved or NotApplied status." };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    companyUser.JoinStatus = joinStatus;
                    // Note: rejectionReason not stored due to schema lacking a field.
                    await companyUserRepo.UpdateAsync(companyUser);
                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

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
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
                if (string.IsNullOrEmpty(userIdClaim))
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
                int userId = int.Parse(userIdClaim);
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "You are not associated with any company." };
                }
                var members = await companyUserRepo.GetApprovedAndInvitedMembersByCompanyIdAsync(companyUser.CompanyId.Value);

                var responses = members.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    Address = m.User?.Profile?.Address,
                    JoinStatus = m.JoinStatus,
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
                Console.WriteLine($"Get self company members error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving company members."
                };
            }
        }

        public async Task<ServiceResponse> GetPendingJoinRequestsSelfAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
            if (string.IsNullOrEmpty(userIdClaim))
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            int userId = int.Parse(userIdClaim);
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not a manager of any company." };

            // Check if trying to update themselves
            if (companyUser.ComUserId == comUserId)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You cannot update your own join request status." };
            }

            // Get the target company user to check their role
            var targetCompanyUser = await companyUserRepo.GetByComUserIdAsync(comUserId);
            if (targetCompanyUser == null)
            {
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Target company user not found." };
            }

            // Check if target user is a manager (roleId = 4)
            if (targetCompanyUser.User?.RoleId == 4)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You cannot update join request status for other managers." };
            }

            var updateResult = await UpdateJoinRequestStatusAsync(companyUser.CompanyId.Value, comUserId, joinStatus);

            // If update succeeded, notify the recruiter about the result
            if (updateResult.Status == SRStatus.Success)
            {
                try
                {
                    var recruiterUserId = targetCompanyUser.UserId;
                    var companyName = targetCompanyUser.Company?.Name ?? "the company";

                    string message;
                    string detail;

                    if (joinStatus == JoinStatusEnum.Approved)
                    {
                        message = $"Your join request has been approved";
                        detail = $"Your request to join '{companyName}' has been approved by the manager.";
                    }
                    else // joinStatus == JoinStatusEnum.NotApplied (treated as rejected)
                    {
                        message = $"Your join request has been rejected";
                        detail = $"Your request to join '{companyName}' has been rejected by the manager.";
                    }

                    await _notificationService.CreateAsync(
                        userId: recruiterUserId,
                        type: NotificationTypeEnum.Member,
                        message: message,
                        detail: detail
                    );
                }
                catch (Exception ex)
                {
                    // Log but do not fail the main operation if notification fails
                    Console.WriteLine($"Error sending join request result notification to recruiter: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }

            return updateResult;
        }

        public async Task<ServiceResponse> CancelJoinRequestAsync()
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

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company user not found." };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.Pending)
                {
                    return new ServiceResponse 
                    { 
                        Status = SRStatus.Forbidden, 
                        Message = "You can only cancel join requests with Pending status." 
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Cancel join request by setting CompanyId to null and JoinStatus to NotApplied
                    companyUser.CompanyId = null;
                    companyUser.JoinStatus = JoinStatusEnum.NotApplied;
                    await companyUserRepo.UpdateAsync(companyUser);
                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse { Status = SRStatus.Success, Message = "Join request canceled successfully." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancel join request error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while canceling join request." };
            }
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
            var userRepo = _uow.GetRepository<IUserRepository>();
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            
            var userEntity = await userRepo.GetByIdAsync(userId);
            if (userEntity == null || userEntity.Role.RoleName != "HR_Manager")
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "Only HR_Manager can perform this action." };
            }
            var managerCompanyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (managerCompanyUser?.CompanyId != companyId)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not a manager of this company." };
            }
            return new ServiceResponse { Status = SRStatus.Success };
        }

        public async Task<ServiceResponse> KickMemberAsync(int comUserId)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
                }

                int currentUserId = int.Parse(userIdClaim);
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var userRepo = _uow.GetRepository<IUserRepository>();

                // Get the company user to be kicked
                var targetCompanyUser = await companyUserRepo.GetByComUserIdAsync(comUserId);
                if (targetCompanyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company member not found."
                    };
                }

                if (targetCompanyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "This user is not associated with any company."
                    };
                }

                int companyId = targetCompanyUser.CompanyId.Value;

                // Get current user's company user record
                var currentCompanyUser = await companyUserRepo.GetByUserIdAsync(currentUserId);
                if (currentCompanyUser == null || currentCompanyUser.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You are not a member of this company."
                    };
                }

                // Get company to check if current user is owner
                var company = await companyRepo.GetByIdAsync(companyId);
                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Check if current user is company owner (CreatedBy is ComUserId)
                bool isOwner = company.CreatedBy == currentCompanyUser.ComUserId;

                // Check if current user is HR_Manager
                var currentUser = await userRepo.GetByIdAsync(currentUserId);
                bool isManager = currentUser != null && currentUser.Role?.RoleName == "HR_Manager";

                // Only owner or manager can kick members
                if (!isOwner && !isManager)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only company owner or manager can remove members."
                    };
                }

                // Prevent kicking yourself
                if (targetCompanyUser.ComUserId == currentCompanyUser.ComUserId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "You cannot remove yourself from the company."
                    };
                }

                // Prevent manager from kicking owner
                if (isManager && !isOwner && company.CreatedBy == targetCompanyUser.ComUserId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Managers cannot remove the company owner."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Remove companyId and set joinStatus to NotApplied
                    targetCompanyUser.CompanyId = null;
                    targetCompanyUser.JoinStatus = JoinStatusEnum.NotApplied;
                    await companyUserRepo.UpdateAsync(targetCompanyUser);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Member removed successfully."
                    };
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kick member error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while removing the member."
                };
            }
        }
    }
}
