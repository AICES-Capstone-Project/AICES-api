using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.Services
{
    public class InvitationService : IInvitationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationService _notificationService;

        public InvitationService(
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor,
            INotificationService notificationService)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _notificationService = notificationService;
        }

        public async Task<ServiceResponse> SendInvitationAsync(string email)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
                }
                int senderId = int.Parse(userIdClaim);

                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var invitationRepo = _uow.GetRepository<IInvitationRepository>();

                // Get sender's company
                var senderCompanyUser = await companyUserRepo.GetByUserIdAsync(senderId);
                if (senderCompanyUser == null || senderCompanyUser.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not associated with any company." };
                }

                // Verify sender is a manager
                var senderUser = await authRepo.GetByEmailAsync(
                    _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "");
                if (senderUser == null || senderUser.RoleId != 4) // 4 = HR_Manager
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "Only HR_Manager can send invitations." };
                }

                // Find receiver by email
                var receiver = await authRepo.GetByEmailAsync(email);
                if (receiver == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "No account found with this email." };
                }

                // Check if receiver is a recruiter (roleId = 5)
                if (receiver.RoleId != 5)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "Can only invite users with HR_Recruiter role." };
                }

                // Check if receiver already belongs to a company
                var receiverCompanyUser = await companyUserRepo.GetByUserIdAsync(receiver.UserId);
                if (receiverCompanyUser != null && receiverCompanyUser.CompanyId != null && receiverCompanyUser.JoinStatus == JoinStatusEnum.Approved)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "This user already belongs to another company." };
                }

                // Check for existing pending invitation
                var hasPendingInvitation = await invitationRepo.HasPendingInvitationAsync(receiver.UserId, senderCompanyUser.CompanyId.Value);
                if (hasPendingInvitation)
                {
                    return new ServiceResponse { Status = SRStatus.Duplicated, Message = "An invitation is already pending for this user." };
                }

                // Get company name for notification
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetByIdAsync(senderCompanyUser.CompanyId.Value);
                var companyName = company?.Name ?? "Unknown Company";
                var senderName = senderUser.Profile?.FullName ?? senderUser.Email;

                await _uow.BeginTransactionAsync();
                try
                {
                    // Create invitation
                    var invitation = new Invitation
                    {
                        SenderId = senderId,
                        ReceiverId = receiver.UserId,
                        CompanyId = senderCompanyUser.CompanyId.Value,
                        Email = email,
                        InvitationStatus = InvitationStatusEnum.Pending
                    };

                    await invitationRepo.AddAsync(invitation);
                    await _uow.SaveChangesAsync();

                    await _uow.CommitTransactionAsync();

                    // Send notification to receiver
                    await _notificationService.CreateWithInvitationAsync(
                        userId: receiver.UserId,
                        type: NotificationTypeEnum.Invitation,
                        message: $"You were invited to join {companyName}",
                        detail: $"{senderName} invited you to join their company.",
                        invitationId: invitation.InvitationId
                    );

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Invitation sent successfully.",
                        Data = new
                        {
                            invitationId = invitation.InvitationId,
                            status = invitation.InvitationStatus.ToString()
                        }
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
                Console.WriteLine($"Send invitation error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while sending invitation." };
            }
        }

        public async Task<ServiceResponse> GetCompanyInvitationsAsync(InvitationStatusEnum? status = null, string? search = null)
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
                var invitationRepo = _uow.GetRepository<IInvitationRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not associated with any company." };
                }

                var invitations = await invitationRepo.GetByCompanyIdAsync(companyUser.CompanyId.Value, status, search);

                var response = invitations.Select(i => new InvitationResponse
                {
                    InvitationId = i.InvitationId,
                    Email = i.Email,
                    ReceiverName = i.Receiver?.Profile?.FullName,
                    SenderName = i.Sender?.Profile?.FullName,
                    CompanyName = i.Company?.Name,
                    CompanyId = i.CompanyId,
                    Status = i.InvitationStatus,
                    CreatedAt = i.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Invitations retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get invitations error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while retrieving invitations." };
            }
        }

        public async Task<ServiceResponse> CancelInvitationAsync(int invitationId)
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
                var invitationRepo = _uow.GetRepository<IInvitationRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You are not associated with any company." };
                }

                var invitation = await invitationRepo.GetForUpdateAsync(invitationId);
                if (invitation == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Invitation not found." };
                }

                if (invitation.CompanyId != companyUser.CompanyId)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "You can only cancel invitations from your company." };
                }

                if (invitation.InvitationStatus != InvitationStatusEnum.Pending)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "Only pending invitations can be cancelled." };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    invitation.InvitationStatus = InvitationStatusEnum.Cancelled;
                    await invitationRepo.UpdateAsync(invitation);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Invitation cancelled successfully."
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
                Console.WriteLine($"Cancel invitation error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while cancelling invitation." };
            }
        }

        public async Task<ServiceResponse> AcceptInvitationAsync(int invitationId)
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
                var invitationRepo = _uow.GetRepository<IInvitationRepository>();

                var invitation = await invitationRepo.GetForUpdateAsync(invitationId);
                if (invitation == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Invitation not found." };
                }

                if (invitation.ReceiverId != userId)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "This invitation is not for you." };
                }

                if (invitation.InvitationStatus != InvitationStatusEnum.Pending)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "This invitation is no longer pending." };
                }

                // Check if user already belongs to a company
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser != null && companyUser.CompanyId != null && companyUser.JoinStatus == JoinStatusEnum.Approved)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "You already belong to a company." };
                }

                var companyName = invitation.Company?.Name ?? "Unknown Company";
                var receiverName = invitation.Receiver?.Profile?.FullName ?? invitation.Email;

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update invitation status
                    invitation.InvitationStatus = InvitationStatusEnum.Accepted;
                    await invitationRepo.UpdateAsync(invitation);

                    // Update or create CompanyUser
                    if (companyUser == null)
                    {
                        companyUser = new CompanyUser
                        {
                            UserId = userId,
                            CompanyId = invitation.CompanyId,
                            JoinStatus = JoinStatusEnum.Approved
                        };
                        await companyUserRepo.AddCompanyUserAsync(companyUser);
                    }
                    else
                    {
                        companyUser.CompanyId = invitation.CompanyId;
                        companyUser.JoinStatus = JoinStatusEnum.Approved;
                        await companyUserRepo.UpdateAsync(companyUser);
                    }

                    await _uow.CommitTransactionAsync();

                    // Send notification to manager (sender)
                    await _notificationService.CreateAsync(
                        userId: invitation.SenderId,
                        type: NotificationTypeEnum.Company,
                        message: $"{receiverName} accepted your invitation",
                        detail: $"{receiverName} has joined {companyName}."
                    );

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Invitation accepted."
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
                Console.WriteLine($"Accept invitation error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while accepting invitation." };
            }
        }

        public async Task<ServiceResponse> DeclineInvitationAsync(int invitationId)
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

                var invitationRepo = _uow.GetRepository<IInvitationRepository>();

                var invitation = await invitationRepo.GetForUpdateAsync(invitationId);
                if (invitation == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Invitation not found." };
                }

                if (invitation.ReceiverId != userId)
                {
                    return new ServiceResponse { Status = SRStatus.Forbidden, Message = "This invitation is not for you." };
                }

                if (invitation.InvitationStatus != InvitationStatusEnum.Pending)
                {
                    return new ServiceResponse { Status = SRStatus.Validation, Message = "This invitation is no longer pending." };
                }

                var companyName = invitation.Company?.Name ?? "Unknown Company";
                var receiverName = invitation.Receiver?.Profile?.FullName ?? invitation.Email;

                await _uow.BeginTransactionAsync();
                try
                {
                    invitation.InvitationStatus = InvitationStatusEnum.Declined;
                    await invitationRepo.UpdateAsync(invitation);
                    await _uow.CommitTransactionAsync();

                    // Send notification to manager (sender)
                    await _notificationService.CreateAsync(
                        userId: invitation.SenderId,
                        type: NotificationTypeEnum.Company,
                        message: $"{receiverName} declined your invitation",
                        detail: $"{receiverName} has declined the invitation to join {companyName}."
                    );

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Invitation declined."
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
                Console.WriteLine($"Decline invitation error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while declining invitation." };
            }
        }
    }
}

