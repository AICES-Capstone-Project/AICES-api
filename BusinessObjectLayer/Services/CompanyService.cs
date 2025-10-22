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
        private readonly ICompanyUserRepository _companyUserRepository;
        private readonly ICompanyDocumentService _companyDocumentService;
        private readonly Cloudinary _cloudinary;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CompanyService(
            ICompanyRepository companyRepository,
            ICompanyUserRepository companyUserRepository,
            ICompanyDocumentService companyDocumentService,
            Cloudinary cloudinary,
            IHttpContextAccessor httpContextAccessor)
        {
            _companyRepository = companyRepository;
            _companyUserRepository = companyUserRepository;
            _companyDocumentService = companyDocumentService;
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
                    (c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
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
                    CompanyStatus = c.CompanyStatus.ToString(),
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
            if (!isSystemManager && company.CompanyStatus != CompanyStatusEnum.Approved)
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
                CompanyStatus = company.CompanyStatus.ToString(),
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
            try
            {
                // ✅ Lấy userId từ claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

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
                    var upload = await UploadFileAsync(request.LogoFile, "companies/logos");
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
                    CompanyStatus = CompanyStatusEnum.Pending,
                    CreatedBy = userId,
                    ApprovedBy = null,
                    RejectReason = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Save company first to get CompanyId
                var createdCompany = await _companyRepository.AddAsync(company);

                // ✅ Upload documents (nếu có) using CompanyDocumentService
                if (request.DocumentFiles != null && request.DocumentFiles.Any())
                {
                    await _companyDocumentService.UploadAndSaveDocumentsAsync(
                        createdCompany.CompanyId,
                        request.DocumentFiles,
                        request.DocumentTypes);
                }

                // ✅ Update CompanyUser with the new CompanyId
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }
                
                else if (companyUser.CompanyId != null || companyUser.JoinStatus == JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "Company user already in a company."
                    };
                }
                
                else
                {
                   companyUser.CompanyId = createdCompany.CompanyId;
                    companyUser.JoinStatus = JoinStatusEnum.Approved;
                    await _companyUserRepository.UpdateAsync(companyUser);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company created successfully. Waiting for admin approval.",
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating company: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the company."
                };
            }
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
                var upload = await UploadFileAsync(request.LogoFile, "companies/logos");
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

        // Upload helper for logo images only
        private async Task<(bool Success, string? Url, string? ErrorMessage)> UploadFileAsync(IFormFile file, string folder)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
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
            company.CompanyStatus = isApproved ? CompanyStatusEnum.Approved : CompanyStatusEnum.Rejected;
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
