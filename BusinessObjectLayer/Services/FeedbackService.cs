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

        public FeedbackService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> CreateAsync(FeedbackRequest request, ClaimsPrincipal userClaims)
        {
            var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
            if (userIdClaim == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

            var userId = int.Parse(userIdClaim);

            // Kiểm tra user có phải HR_Recruiter hoặc HR_Manager không
            var userRepo = _uow.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(userId);
            if (user == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "User not found" };

            var roleRepo = _uow.GetRepository<IRoleRepository>();
            var role = await roleRepo.GetByIdAsync(user.RoleId);
            if (role == null)
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Role not found" };

            // Chỉ HR_Recruiter và HR_Manager mới được tạo feedback
            if (role.RoleName != "HR_Recruiter" && role.RoleName != "HR_Manager")
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Only HR_Recruiter and HR_Manager can create feedback."
                };
            }

            // Lấy CompanyUser của user
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

            var feedbackRepo = _uow.GetRepository<IFeedbackRepository>();

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

            var responseData = feedbacks.Select(f => new FeedbackResponse
            {
                FeedbackId = f.FeedbackId,
                UserName = f.CompanyUser?.User?.Email ?? string.Empty,
                Rating = f.Rating,
                CreatedAt = f.CreatedAt
            }).ToList();

            var result = new
            {
                Feedbacks = responseData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize,
                Total = total
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
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                CreatedAt = feedback.CreatedAt
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Feedback retrieved successfully.",
                Data = response
            };
        }
    }
}
