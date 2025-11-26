using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _uow;
        private readonly IProfileService _profileService;
        private readonly ICompanyUserService _companyUserService;

        public UserService(
            IUnitOfWork uow, 
            IProfileService profileService, 
            ICompanyUserService companyUserService)
        {
            _uow = uow;
            _profileService = profileService;
            _companyUserService = companyUserService;
        }

        public async Task<ServiceResponse> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var userRepo = _uow.GetRepository<IUserRepository>();
            var users = await userRepo.GetUsersAsync(page, pageSize, search);
            var total = await userRepo.GetTotalUsersAsync(search);

            var userResponses = users.Select(u => new UserResponse
            {
                UserId = u.UserId,
                Email = u.Email,
                RoleName = u.Role?.RoleName ?? "Unknown",
                FullName = u.Profile?.FullName ?? "",
                Address = u.Profile?.Address ?? "",
                DateOfBirth = u.Profile?.DateOfBirth,
                AvatarUrl = u.Profile?.AvatarUrl ?? "",
                PhoneNumber = u.Profile?.PhoneNumber ?? "",
                UserStatus = u.Status.ToString(),
                CreatedAt = u.CreatedAt,
                LoginProviders = u.LoginProviders?.Select(lp => new LoginProviderInfo
                {
                    AuthProvider = lp.AuthProvider,
                    ProviderId = lp.ProviderId,
                }).ToList() ?? new List<LoginProviderInfo>()
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

        public async Task<ServiceResponse> GetUserByIdAsync(int id)
        {
            var userRepo = _uow.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(id);
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
                Data = new UserResponse
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    RoleName = user.Role?.RoleName ?? "Unknown",
                    FullName = user.Profile?.FullName ?? "",
                    Address = user.Profile?.Address ?? "",
                    DateOfBirth = user.Profile?.DateOfBirth,
                    AvatarUrl = user.Profile?.AvatarUrl ?? "",
                    PhoneNumber = user.Profile?.PhoneNumber ?? "",
                    CompanyName = user.CompanyUser?.Company?.Name ?? "",
                    JoinStatus = user.CompanyUser?.JoinStatus.ToString() ?? "",
                    UserStatus = user.Status.ToString(),
                    CreatedAt = user.CreatedAt,
                    LoginProviders = user.LoginProviders?.Select(lp => new LoginProviderInfo
                    {
                        AuthProvider = lp.AuthProvider,
                        ProviderId = lp.ProviderId,
                    }).ToList() ?? new List<LoginProviderInfo>()
                }
            };
        }

        public async Task<ServiceResponse> CreateUserAsync(UserRequest request)
        {
            var userRepo = _uow.GetRepository<IUserRepository>();
            
            if (await userRepo.EmailExistsAsync(request.Email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Email already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    Email = request.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    RoleId = request.RoleId,
                    Status = UserStatusEnum.Verified,
                };

                await userRepo.AddAsync(user);
                await _uow.SaveChangesAsync(); // Get UserId

                // Lưu default LoginProvider cho "Local"
                var localProvider = new LoginProvider
                {
                    UserId = user.UserId,
                    AuthProvider = AuthProviderEnum.Local,
                    ProviderId = "" // Không cần ProviderId cho Local
                };
                await userRepo.AddLoginProviderAsync(localProvider);
                await _uow.SaveChangesAsync();

                // Tạo profile mặc định
                await _profileService.CreateDefaultProfileAsync(user.UserId, request.FullName ?? request.Email);

                // Create default company user for roleId 4 (HR_Manager) and 5 (HR_Recruiter)
                if (request.RoleId == 4 || request.RoleId == 5)
                {
                    var companyUserResult = await _companyUserService.CreateDefaultCompanyUserAsync(user.UserId);
                    if (companyUserResult.Status != SRStatus.Success)
                    {
                        // Log the error but don't fail the user creation
                        Console.WriteLine($"Warning: Failed to create default company user: {companyUserResult.Message}");
                    }
                }

                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "User created successfully.",
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }
        public async Task<ServiceResponse> UpdateUserAsync(int id, UserRequest request)
        {
            var userRepo = _uow.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(id);
            if (user == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "User not found."
                };
            }

            if (user.Email != request.Email && await userRepo.EmailExistsAsync(request.Email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Email already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                user.Email = request.Email;
                user.RoleId = request.RoleId;
                if (!string.IsNullOrEmpty(request.Password))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }

                await userRepo.UpdateAsync(user);
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "User updated successfully.",
            };
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            try
            {
                var userRepo = _uow.GetRepository<IUserRepository>();
                var profileRepo = _uow.GetRepository<IProfileRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                
                var user = await userRepo.GetByIdAsync(id);
                if (user == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "User not found."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Soft delete user
                    user.IsActive = false;
                    await userRepo.UpdateAsync(user);

                    // Soft delete profile if exists (using navigation property)
                    if (user.Profile != null)
                    {
                        user.Profile.IsActive = false;
                        await profileRepo.UpdateAsync(user.Profile);
                    }

                    // Soft delete company user if exists (using navigation property)
                    if (user.CompanyUser != null)
                    {
                        user.CompanyUser.IsActive = false;
                        await companyUserRepo.UpdateAsync(user.CompanyUser);
                    }

                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "User deleted successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error soft deleting user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting the user."
                };
            }
        }

        public async Task<ServiceResponse> UpdateUserStatusAsync(int id, UserStatusEnum status)
        {
            try
            {
                var userRepo = _uow.GetRepository<IUserRepository>();
                var user = await userRepo.GetByIdAsync(id);
                if (user == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "User not found."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update user status
                    user.Status = status;
                    await userRepo.UpdateAsync(user);
                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = $"User status updated to {status} successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user status: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating user status."
                };
            }
        }
    }
}
