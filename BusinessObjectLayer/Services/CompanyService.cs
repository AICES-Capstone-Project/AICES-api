using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
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
        private readonly IUserRepository _userRepository;
        private readonly Cloudinary _cloudinary;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CompanyService(
            ICompanyRepository companyRepository,
            ICompanyUserRepository companyUserRepository,
            ICompanyDocumentService companyDocumentService,
            IUserRepository userRepository,
            Cloudinary cloudinary,
            IHttpContextAccessor httpContextAccessor)
        {
            _companyRepository = companyRepository;
            _companyUserRepository = companyUserRepository;
            _companyDocumentService = companyDocumentService;
            _userRepository = userRepository;
            _cloudinary = cloudinary;
            _httpContextAccessor = httpContextAccessor;
        }

        // Get public companies (active and approved only)
        public async Task<ServiceResponse> GetPublicAsync()
        {
            try
            {
                var companies = await _companyRepository.GetPublicCompaniesAsync();

                var companyResponses = companies.Select(c => new PublicCompanyResponse
                {
                    CompanyId = c.CompanyId,
                    Name = c.Name,
                    Description = c.Description,
                    Address = c.Address,
                    WebsiteUrl = c.Website,
                    LogoUrl = c.LogoUrl
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Public companies retrieved successfully.",
                    Data = companyResponses
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get public companies error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving public companies."
                };
            }
        }

        // Get public company by ID (active and approved only)
        public async Task<ServiceResponse> GetPublicByIdAsync(int id)
        {
            try
            {
                var company = await _companyRepository.GetPublicByIdAsync(id);

                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found or not available."
                    };
                }

                var companyResponse = new PublicCompanyResponse
                {
                    CompanyId = company.CompanyId,
                    Name = company.Name,
                    Description = company.Description,
                    Address = company.Address,
                    WebsiteUrl = company.Website,
                    LogoUrl = company.LogoUrl
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Public company retrieved successfully.",
                    Data = companyResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get public company by id error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the company."
                };
            }
        }

        // Get all companies with pagination and search (for admins)
        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var companies = await _companyRepository.GetCompaniesAsync(page, pageSize, search);
                var total = await _companyRepository.GetTotalCompaniesAsync(search);

                var companyResponses = companies.Select(c => new CompanyResponse
                {
                    CompanyId = c.CompanyId,
                    Name = c.Name,
                    Description = c.Description,
                    Address = c.Address,
                    WebsiteUrl = c.Website,
                    LogoUrl = c.LogoUrl,
                    CompanyStatus = c.CompanyStatus.ToString(),
                    CreatedBy = c.CreatedBy,
                    ApprovalBy = c.ApprovedBy,
                    RejectionReason = c.RejectReason,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Companies retrieved successfully.",
                    Data = new PaginatedCompanyResponse
                    {
                        Companies = companyResponses,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get companies error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving companies."
                };
            }
        }

        // Get by Id (for admins)
        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            try
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

                var companyResponse = new CompanyResponse
                {
                    CompanyId = company.CompanyId,
                    Name = company.Name,
                    Description = company.Description,
                    Address = company.Address,
                    WebsiteUrl = company.Website,
                    LogoUrl = company.LogoUrl,
                    CompanyStatus = company.CompanyStatus.ToString(),
                    CreatedBy = company.CreatedBy,
                    ApprovalBy = company.ApprovedBy,
                    RejectionReason = company.RejectReason,
                    IsActive = company.IsActive,
                    CreatedAt = company.CreatedAt
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company retrieved successfully.",
                    Data = companyResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get company by id error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the company."
                };
            }
        }



        // Create company for System Admin (automatically approved)
        public async Task<ServiceResponse> CreateAsync(CompanyRequest request)
        {
            try
            {
                // Get current admin user ID from claims for ApprovedBy
                var user = _httpContextAccessor.HttpContext?.User;
                var adminUserIdClaim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(adminUserIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Admin user not authenticated."
                    };
                }

                int adminUserId = int.Parse(adminUserIdClaim);

                // ✅ Validate documents for POST (required)
                if (request.DocumentFiles == null || !request.DocumentFiles.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "At least one document file is required."
                    };
                }

                if (request.DocumentTypes == null || !request.DocumentTypes.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Document types are required."
                    };
                }

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

                // ✅ Tạo công ty mới với status Approved
                var company = new Company
                {
                    Name = request.Name,
                    Description = request.Description,
                    Address = request.Address,
                    Website = request.Website,
                    LogoUrl = logoUrl,
                    CompanyStatus = CompanyStatusEnum.Approved, // Automatically approved
                    CreatedBy = adminUserId, // User who owns the company
                    ApprovedBy = adminUserId, // Admin who approved/created it
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

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company created successfully.",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating company for system: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the company."
                };
            }
        }

 // Get self company (for HR users to view their own company)
        public async Task<ServiceResponse> GetSelfCompanyAsync()
        {
            try
            {
                // Get current user ID from claims
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

                // Get company user to find associated company
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
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

                var company = await _companyRepository.GetByIdAsync(companyUser.CompanyId.Value);

                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                var companyResponse = new SelfCompanyResponse
                {
                    CompanyId = company.CompanyId,
                    Name = company.Name,
                    Description = company.Description,
                    Address = company.Address,
                    WebsiteUrl = company.Website,
                    LogoUrl = company.LogoUrl,
                    CompanyStatus = company.CompanyStatus,
                    RejectionReason = company.RejectReason,
                    Documents = company.CompanyDocuments?.Select(d => new CompanyDocumentResponse
                    {
                        DocumentType = d.DocumentType ?? "",
                        FileUrl = d.FileUrl ?? ""
                    }).ToList() ?? new List<CompanyDocumentResponse>()
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company retrieved successfully.",
                    Data = companyResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get self company error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving your company."
                };
            }
        }
        
        // Create company (HR user creates for themselves)
        public async Task<ServiceResponse> SelfCreateAsync(CompanyRequest request)
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

                // ✅ Check if user already belongs to a different company BEFORE creating
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                // Check if user is already associated with a different company
                if (companyUser.CompanyId != null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "You are already associated with another company. Please leave your current company before creating a new one."
                    };
                }

                // ✅ Validate documents for POST (required)
                if (request.DocumentFiles == null || !request.DocumentFiles.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "At least one document file is required."
                    };
                }

                if (request.DocumentTypes == null || !request.DocumentTypes.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Document types are required."
                    };
                }

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

                // Associate user with the newly created company
                companyUser.CompanyId = createdCompany.CompanyId;
                companyUser.JoinStatus = JoinStatusEnum.Approved;
                await _companyUserRepository.UpdateAsync(companyUser);

                // Update user roleId from 5 (HR_Recruiter) to 4 (HR_Manager)
                var userEntity = await _userRepository.GetByIdAsync(userId);
                if (userEntity != null && userEntity.RoleId == 5)
                {
                    userEntity.RoleId = 4; // Change from HR_Recruiter to HR_Manager
                    await _userRepository.UpdateAsync(userEntity);
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company created successfully. Waiting for admin approval.",
                };
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




        // Update self company (for HR users to update their own company)
        public async Task<ServiceResponse> UpdateSelfCompanyAsync(CompanyRequest request)
        {
            try
            {
                // Get current user ID from claims
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

                // Get company user to find associated company
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
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

                var company = await _companyRepository.GetByIdAsync(companyUser.CompanyId.Value);
                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Update allowed fields
                if (!string.IsNullOrEmpty(request.Name))
                    company.Name = request.Name;
                if (!string.IsNullOrEmpty(request.Description))
                    company.Description = request.Description;
                if (!string.IsNullOrEmpty(request.Address))
                    company.Address = request.Address;
                if (!string.IsNullOrEmpty(request.Website))
                    company.Website = request.Website;

                // Upload new logo if provided
                if (request.LogoFile != null)
                {
                    var upload = await UploadFileAsync(request.LogoFile, "companies/logos");
                    if (!upload.Success)
                        return new ServiceResponse { Status = SRStatus.Error, Message = upload.ErrorMessage };
                    company.LogoUrl = upload.Url;
                }

                // Set status to Pending when company is updated
                company.CompanyStatus = CompanyStatusEnum.Pending;
                company.ApprovedBy = null; // Clear approval
                company.RejectReason = null; // Clear rejection reason

                // CreatedBy remains unchanged (already set)

                await _companyRepository.UpdateAsync(company);

                // Upload new documents if provided
                if (request.DocumentFiles != null && request.DocumentFiles.Any())
                {
                    await _companyDocumentService.UploadAndSaveDocumentsAsync(
                        company.CompanyId,
                        request.DocumentFiles,
                        request.DocumentTypes);
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company updated successfully. Status set to Pending for admin review."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update self company error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating your company."
                };
            }
        }

        // Update (for admins)
        public async Task<ServiceResponse> UpdateAsync(int id, CompanyRequest request)
        {
            try
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

                // Update allowed fields
                if (!string.IsNullOrEmpty(request.Name))
                    company.Name = request.Name;
                if (!string.IsNullOrEmpty(request.Description))
                    company.Description = request.Description;
                if (!string.IsNullOrEmpty(request.Address))
                    company.Address = request.Address;
                if (!string.IsNullOrEmpty(request.Website))
                    company.Website = request.Website;

                // Upload new logo if provided
                if (request.LogoFile != null)
                {
                    var upload = await UploadFileAsync(request.LogoFile, "companies/logos");
                    if (!upload.Success)
                        return new ServiceResponse { Status = SRStatus.Error, Message = upload.ErrorMessage };
                    company.LogoUrl = upload.Url;
                }

                await _companyRepository.UpdateAsync(company);

                // Upload new documents if provided
                if (request.DocumentFiles != null && request.DocumentFiles.Any())
                {
                    await _companyDocumentService.UploadAndSaveDocumentsAsync(
                        company.CompanyId,
                        request.DocumentFiles,
                        request.DocumentTypes);
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company updated successfully. Status set to Pending for admin review."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update company error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the company."
                };
            }
        }

        // Update company profile only (without changing status)
        public async Task<ServiceResponse> UpdateCompanyProfileAsync(int id, CompanyProfileUpdateRequest request)
        {
            try
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

                // Update only profile fields (does NOT change status)
                if (!string.IsNullOrEmpty(request.Description))
                    company.Description = request.Description;
                if (!string.IsNullOrEmpty(request.Address))
                    company.Address = request.Address;
                if (!string.IsNullOrEmpty(request.Website))
                    company.Website = request.Website;

                // Upload new logo if provided
                if (request.LogoFile != null)
                {
                    var upload = await UploadFileAsync(request.LogoFile, "companies/logos");
                    if (!upload.Success)
                        return new ServiceResponse { Status = SRStatus.Error, Message = upload.ErrorMessage };
                    company.LogoUrl = upload.Url;
                }

                // Status, ApprovedBy, CreatedBy, RejectReason remain UNCHANGED

                await _companyRepository.UpdateAsync(company);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company profile updated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update company profile error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the company profile."
                };
            }
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

        public async Task<ServiceResponse> UpdateCompanyStatusAsync(int companyId, CompanyStatusEnum status, string? rejectionReason = null)
        {
            try
            {
                // Get current user ID from claims
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

                int currentUserId = int.Parse(userIdClaim);

                var company = await _companyRepository.GetByIdAsync(companyId);

                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Update company status
                company.CompanyStatus = status;

                // Handle status-specific logic
                switch (status)
                {
                    case CompanyStatusEnum.Approved:
                        // Set approvedBy and clear rejection reason
                        company.ApprovedBy = currentUserId;
                        company.RejectReason = null;
                        
                        await _companyRepository.UpdateAsync(company);

                        // Promote user to HR_Manager
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

                    case CompanyStatusEnum.Rejected:
                        // Set rejection reason and clear approvedBy
                        company.RejectReason = rejectionReason;
                        company.ApprovedBy = null;
                        
                        await _companyRepository.UpdateAsync(company);

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company rejected."
                        };

                    case CompanyStatusEnum.Pending:
                        // Clear both approvedBy and rejection reason
                        company.ApprovedBy = null;
                        company.RejectReason = null;
                        
                        await _companyRepository.UpdateAsync(company);

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company status set to pending."
                        };

                    case CompanyStatusEnum.Suspended:
                        // Keep existing approvedBy and rejection reason
                        await _companyRepository.UpdateAsync(company);

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company suspended."
                        };

                    case CompanyStatusEnum.Canceled:
                        // Keep existing approvedBy and rejection reason
                        await _companyRepository.UpdateAsync(company);

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company canceled."
                        };

                    default:
                        return new ServiceResponse
                        {
                            Status = SRStatus.Error,
                            Message = "Invalid company status."
                        };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating company status: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating company status."
                };
            }
        }
    }
}
