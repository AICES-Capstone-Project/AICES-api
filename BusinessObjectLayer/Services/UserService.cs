using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IProfileService _profileService;

        public UserService(IUserRepository userRepository, IProfileService profileService)
        {
            _userRepository = userRepository;
            _profileService = profileService;
        }

        public async Task<ServiceResponse> CreateUserAsync(UserRequest request)
        {
            if (await _userRepository.EmailExistsAsync(request.Email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Email already exists."
                };
            }

            var user = new User
            {
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = request.RoleId,
                IsActive = request.IsActive ?? true
            };

            var addedUser = await _userRepository.AddAsync(user);

            // Lưu default LoginProvider cho "Local"
            var localProvider = new LoginProvider
            {
                UserId = addedUser.UserId,
                AuthProvider = "Local",
                ProviderId = "" // Không cần ProviderId cho Local
            };
            await _userRepository.AddLoginProviderAsync(localProvider);

            // Tạo profile mặc định
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId, request.FullName ?? request.Email);

            string roleName = request.RoleId switch
            {
                1 => "Admin",
                2 => "Manager",
                3 => "Recruiter",
                _ => "Candidate"
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "User created successfully.",
                Data = new UserAdminResponse
                {
                    UserId = addedUser.UserId,
                    Email = addedUser.Email,
                    RoleName = roleName,
                    FullName = request.FullName,
                    IsActive = addedUser.IsActive,
                    CreatedAt = (DateTime)addedUser.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "User not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "User retrieved successfully.",
                Data = new UserAdminResponse
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    RoleName = user.Role?.RoleName ?? "Unknown",
                    FullName = user.Profile?.FullName ?? "",
                    IsActive = user.IsActive,
                    CreatedAt = (DateTime)user.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var users = await _userRepository.GetUsersAsync(page, pageSize, search);
            var total = await _userRepository.GetTotalUsersAsync(search);

            var userResponses = users.Select(u => new UserAdminResponse
            {
                UserId = u.UserId,
                Email = u.Email,
                RoleName = u.Role?.RoleName ?? "Unknown",
                FullName = u.Profile?.FullName ?? "",
                IsActive = u.IsActive,
                CreatedAt = (DateTime)u.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Users retrieved successfully.",
                Data = new PaginatedUserResponse
                {
                    Users = userResponses,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize
                }
            };
        }

        public async Task<ServiceResponse> UpdateUserAsync(int id, UserRequest request)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "User not found."
                };
            }

            if (user.Email != request.Email && await _userRepository.EmailExistsAsync(request.Email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Email already exists."
                };
            }

            user.Email = request.Email;
            user.RoleId = request.RoleId;
            if (!string.IsNullOrEmpty(request.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }
            user.IsActive = request.IsActive ?? true; 

            await _userRepository.UpdateAsync(user);

            string roleName = request.RoleId switch
            {
                1 => "Admin",
                2 => "Manager",
                3 => "Recruiter",
                _ => "Candidate"
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "User updated successfully.",
                Data = new UserAdminResponse
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    RoleName = roleName,
                    FullName = user.Profile?.FullName ?? "",
                    IsActive = user.IsActive,
                    CreatedAt = (DateTime)user.CreatedAt
                }
            };
        }

       
    }
}
