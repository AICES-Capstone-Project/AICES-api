using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _uow;
        private readonly IContentValidationService _contentValidationService;

        public FeedbackService(IUnitOfWork uow, IContentValidationService contentValidationService)
        {
            _uow = uow;
            _contentValidationService = contentValidationService;
        }

        public async Task<ServiceResponse> CreateAsync(FeedbackRequest request, ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            
            var userRepo = _uow.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(userId);
            if (user == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "User not found" };

            var roleRepo = _uow.GetRepository<IRoleRepository>();
            var role = await roleRepo.GetByIdAsync(user.RoleId);
            if (role == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Role not found" };

           
            if (role.RoleName != "HR_Recruiter" && role.RoleName != "HR_Manager")
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Only HR_Recruiter and HR_Manager can create feedback."
                };
            }

            
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null || companyUser.CompanyId == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "You must join a company before creating feedback."
                };
            }

            // ============================================
            // RATE LIMITING: Only 1 feedback per week
            // ============================================
            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var hasRecentFeedback = await feedbackRepo.HasRecentFeedbackAsync(companyUser.ComUserId, oneWeekAgo);
            
            if (hasRecentFeedback)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "You can only submit one feedback per week."
                };
            }

            // ============================================
            // VALIDATE CONTENT USING GOOGLE CLOUD NLP
            // ============================================
            
            // Validate Comment (if provided, needs 3 meaningful words)
            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                var (isCommentValid, commentError) = await _contentValidationService
                    .ValidateJobContentAsync(request.Comment, "Feedback Comment", minMeaningfulTokens: 3);
                if (!isCommentValid)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = commentError
                    };
                }
            }

            var feedback = new Feedback
            {
                ComUserId = companyUser.ComUserId,
                Rating = request.Rating,
                Comment = request.Comment
            };

            await feedbackRepo.AddAsync(feedback);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedback created successfully.",
            };
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10)
        {
            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var feedbacks = await feedbackRepo.GetAllFeedbacksAsync(page, pageSize);
            var total = await feedbackRepo.GetTotalFeedbacksAsync();

            var responseData = feedbacks.Select(f => new FeedbackDetailResponse
            {
                FeedbackId = f.FeedbackId,
                UserName = f.CompanyUser?.User?.Email ?? string.Empty,
                Rating = f.Rating,
                CreatedAt = f.CreatedAt,
                ComUserId = f.ComUserId,
                CompanyName = f.CompanyUser?.Company?.Name,
                CompanyId = f.CompanyUser?.CompanyId,
                CompanyLogoUrl = f.CompanyUser?.Company?.LogoUrl,
                UserEmail = f.CompanyUser?.User?.Email,
                UserFullName = f.CompanyUser?.User?.Profile?.FullName,
                UserAvatarUrl = f.CompanyUser?.User?.Profile?.AvatarUrl
            }).ToList();

            var result = new
            {
                Feedbacks = responseData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedbacks retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int feedbackId)
        {
            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var feedback = await feedbackRepo.GetByIdAsync(feedbackId);

            if (feedback == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Feedback not found."
                };
            }

            var response = new FeedbackDetailResponse
            {
                FeedbackId = feedback.FeedbackId,
                ComUserId = feedback.ComUserId,
                UserName = feedback.CompanyUser?.User?.Email ?? string.Empty,
                UserEmail = feedback.CompanyUser?.User?.Email,
                UserFullName = feedback.CompanyUser?.User?.Profile?.FullName,
                CompanyName = feedback.CompanyUser?.Company?.Name,
                CompanyId = feedback.CompanyUser?.CompanyId,
                CompanyLogoUrl = feedback.CompanyUser?.Company?.LogoUrl,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt,
                UserAvatarUrl = feedback.CompanyUser?.User?.Profile?.AvatarUrl
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedback retrieved successfully.",
                Data = response
            };
        }

        public async Task<ServiceResponse> GetMyFeedbacksAsync(ClaimsPrincipal userClaims, int page = 1, int pageSize = 10)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

            if (companyUser == null || companyUser.CompanyId == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "You must join a company before viewing feedbacks."
                };
            }

            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var feedbacks = await feedbackRepo.GetFeedbacksByComUserIdAsync(companyUser.ComUserId, page, pageSize);
            var total = await feedbackRepo.GetTotalFeedbacksByComUserIdAsync(companyUser.ComUserId);

            var responseData = feedbacks.Select(f => new FeedbackDetailResponse
            {
                FeedbackId = f.FeedbackId,
                ComUserId = f.ComUserId,
                UserName = f.CompanyUser?.User?.Email ?? string.Empty,
                UserEmail = f.CompanyUser?.User?.Email,
                UserFullName = f.CompanyUser?.User?.Profile?.FullName,
                CompanyName = f.CompanyUser?.Company?.Name,
                CompanyId = f.CompanyUser?.CompanyId,
                CompanyLogoUrl = f.CompanyUser?.Company?.LogoUrl,
                Rating = f.Rating,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                UserAvatarUrl = f.CompanyUser?.User?.Profile?.AvatarUrl
            }).ToList();

            var result = new
            {
                Feedbacks = responseData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize,
                Total = total,
                TotalCount = total
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedbacks retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> DeleteMyFeedbackAsync(int feedbackId, ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

            if (companyUser == null || companyUser.CompanyId == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "You must join a company."
                };
            }

            
            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var feedback = await feedbackRepo.GetForUpdateAsync(feedbackId);

            if (feedback == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Feedback not found."
                };
            }

            
            if (feedback.ComUserId != companyUser.ComUserId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "You can only delete your own feedback."
                };
            }

            
            await feedbackRepo.SoftDeleteAsync(feedback);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedback deleted successfully."
            };
        }

        public async Task<ServiceResponse> DeleteFeedbackByAdminAsync(int feedbackId)
        {
            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();
            var feedback = await feedbackRepo.GetForUpdateAsync(feedbackId);

            if (feedback == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Feedback not found."
                };
            }

           
            await feedbackRepo.SoftDeleteAsync(feedback);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedback deleted successfully by admin."
            };
        }
    }
}
