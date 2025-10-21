using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly Cloudinary _cloudinary;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CompanyService(ICompanyRepository companyRepository, Cloudinary cloudinary, IHttpContextAccessor httpContextAccessor)
        {
            _companyRepository = companyRepository;
            _cloudinary = cloudinary;
            _httpContextAccessor = httpContextAccessor;
        }

        // Get all active
        public async Task<ServiceResponse> GetAllAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userRole =
                user?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ??
                user?.FindFirst("role")?.Value ??
                user?.FindFirst("roles")?.Value ??
                user?.FindFirst("RoleName")?.Value;

            bool isSystemManager = userRole == "System_Manager" || userRole == "System_Admin";

            var companies = await _companyRepository.GetAllAsync(includeInactive: isSystemManager);

            var filteredCompanies = companies
                .Where(c =>
                    isSystemManager ||
                    (c.IsActive && c.ApprovalStatus == ApprovalStatusEnum.Approved)
                )
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CompanyResponse
                {
                    CompanyId = c.CompanyId,
                    Name = c.Name,
                    Description = c.Description,
                    Address = c.Address,
                    Website = c.Website,
                    LogoUrl = c.LogoUrl,
                    ApprovalStatus = c.ApprovalStatus.ToString(),
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt
                })
                .ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Companies retrieved successfully.",
                Data = filteredCompanies
            };
        }





        // Get by Id
        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var user = _httpContextAccessor.HttpContext?.User;

            // Lấy đúng claim role từ JWT
            var userRole =
                user?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ??
                user?.FindFirst("role")?.Value ??
                user?.FindFirst("roles")?.Value ??
                user?.FindFirst("RoleName")?.Value;

            bool isSystemManager = userRole == "System_Manager" || userRole == "System_Admin";

            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            // Chỉ cho phép xem nếu:
            // - là System_Manager/Admin, hoặc
            // - company đã được Approved
            if (!isSystemManager && company.ApprovalStatus != ApprovalStatusEnum.Approved)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "You are not allowed to view this company."
                };
            }

            var data = new CompanyResponse
            {
                CompanyId = company.CompanyId,
                Name = company.Name,
                Description = company.Description,
                Address = company.Address,
                Website = company.Website,
                LogoUrl = company.LogoUrl,
                ApprovalStatus = company.ApprovalStatus.ToString(),
                IsActive = company.IsActive,
                CreatedAt = company.CreatedAt
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company retrieved successfully.",
                Data = data
            };
        }



        // Create
        public async Task<ServiceResponse> CreateAsync(CompanyRequest request)
        {
            // ✅ Lấy userId từ JWT token
            var userIdClaim =
     _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ??
     _httpContextAccessor.HttpContext?.User.FindFirst("nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                };
            }

            int userId = int.Parse(userIdClaim);

            // ✅ Kiểm tra trùng tên công ty
            if (await _companyRepository.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Company name already exists."
                };
            }

            // ✅ Upload logo (nếu có)
            string? logoUrl = null;
            if (request.LogoFile != null)
            {
                var upload = await UploadLogoAsync(request.LogoFile);
                if (!upload.Success)
                    return new ServiceResponse { Status = SRStatus.Error, Message = upload.ErrorMessage };
                logoUrl = upload.Url;
            }

            // ✅ Tạo công ty mới
            var company = new Company
            {
                Name = request.Name,
                Description = request.Description,
                Address = request.Address,
                Website = request.Website,
                LogoUrl = logoUrl,
                ApprovalStatus = ApprovalStatusEnum.Pending, // Công ty vẫn pending
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _companyRepository.AddAsync(company);

            // ✅ Thêm người tạo vào CompanyUsers với JoinStatus = Approved
            var companyUser = new CompanyUser
            {
                UserId = userId,
                CompanyId = company.CompanyId,
                RoleId = 4, // hoặc để mặc định nếu bạn không cần dùng RoleId
                JoinStatus = JoinStatusEnum.Approved, // ✅ Mặc định Approved
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _companyRepository.AddCompanyUserAsync(companyUser);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company created successfully and creator added with Approved join status."
            };
        }




        // Update
        public async Task<ServiceResponse> UpdateAsync(int id, CompanyRequest request)
        {
            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            if (!string.IsNullOrEmpty(request.Name))
                company.Name = request.Name;
            if (!string.IsNullOrEmpty(request.Description))
                company.Description = request.Description;
            if (!string.IsNullOrEmpty(request.Address))
                company.Address = request.Address;
            if (!string.IsNullOrEmpty(request.Website))
                company.Website = request.Website;

            if (request.LogoFile != null)
            {
                var upload = await UploadLogoAsync(request.LogoFile);
                if (!upload.Success)
                    return new ServiceResponse { Status = SRStatus.Error, Message = upload.ErrorMessage };
                company.LogoUrl = upload.Url;
            }

            await _companyRepository.UpdateAsync(company);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company updated successfully."
            };
        }

        // Soft delete
        public async Task<ServiceResponse> DeleteAsync(int id)
        {
            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            company.IsActive = false;
            await _companyRepository.UpdateAsync(company);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company deactivated successfully."
            };
        }

        // Upload helper
        private async Task<(bool Success, string? Url, string? ErrorMessage)> UploadLogoAsync(IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "companies",
                    Transformation = new Transformation().Width(400).Height(400).Crop("fill")
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null)
                    return (false, null, result.Error.Message);
                return (true, result.SecureUrl.ToString(), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<ServiceResponse> ApproveOrRejectAsync(int companyId, bool isApproved)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);

            if (company == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            // Cập nhật trạng thái duyệt
            company.ApprovalStatus = isApproved ? ApprovalStatusEnum.Approved : ApprovalStatusEnum.Rejected;
            await _companyRepository.UpdateAsync(company);

            if (isApproved)
            {
                bool updated = await _companyRepository.UpdateUserRoleByCompanyAsync(companyId, "HR_Manager");

                if (!updated)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Company approved, but failed to update user role."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company approved and user promoted to HR_Manager."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Company rejected."
            };
        }





    }
}
