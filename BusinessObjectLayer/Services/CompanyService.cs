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
        private readonly Common.CloudinaryHelper _cloudinaryHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CompanyService(
            ICompanyRepository companyRepository,
            ICompanyUserRepository companyUserRepository,
            ICompanyDocumentService companyDocumentService,
            IUserRepository userRepository,
            Common.CloudinaryHelper cloudinaryHelper,
            IHttpContextAccessor httpContextAccessor)
        {
            _companyRepository = companyRepository;
            _companyUserRepository = companyUserRepository;
            _companyDocumentService = companyDocumentService;
            _userRepository = userRepository;
            _cloudinaryHelper = cloudinaryHelper;
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
                    TaxCode = c.TaxCode,
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
                    TaxCode = company.TaxCode,
                    LogoUrl = company.LogoUrl,
                    CompanyStatus = company.CompanyStatus.ToString(),
                    CreatedBy = company.CreatedBy,
                    ApprovalBy = company.ApprovedBy,
                    RejectionReason = company.RejectReason,
                    IsActive = company.IsActive,
                    CreatedAt = company.CreatedAt,
                    Documents = company.CompanyDocuments?.Select(d => new CompanyDocumentResponse
                    {
                        DocumentType = d.DocumentType ?? string.Empty,
                        FileUrl = d.FileUrl ?? string.Empty
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
                var adminUserIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

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
                if (request.DocumentFiles == null || request.DocumentFiles.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one document file is required."
                    };
                }

                if (request.DocumentTypes == null || request.DocumentTypes.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
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
                    TaxCode = request.TaxCode,
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
                if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
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
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

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
                    TaxCode = company.TaxCode,
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
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

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
                if (request.DocumentFiles == null || request.DocumentFiles.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one document file is required."
                    };
                }

                if (request.DocumentTypes == null || request.DocumentTypes.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
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
                    TaxCode = request.TaxCode,
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
                if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
                {
                    await _companyDocumentService.UploadAndSaveDocumentsAsync(
                        createdCompany.CompanyId,
                        request.DocumentFiles,
                        request.DocumentTypes);
                }

                // Associate user with the newly created company
                companyUser.CompanyId = createdCompany.CompanyId;
                await _companyUserRepository.UpdateAsync(companyUser);

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
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

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
                if (!string.IsNullOrEmpty(request.TaxCode))
                    company.TaxCode = request.TaxCode;

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
                if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
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
            return await _cloudinaryHelper.UploadCompanyLogoAsync(file);
        }

        public async Task<ServiceResponse> UpdateCompanyStatusAsync(int companyId, CompanyStatusEnum status, string? rejectionReason = null)
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int currentUserId = int.Parse(userIdClaim);

                // Check if user has required roles (System_Admin or System_Manager)
                if (user == null || (!user.IsInRole("System_Admin") && !user.IsInRole("System_Manager")))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only System_Admin and System_Manager can update company status."
                    };
                }

                var company = await _companyRepository.GetByIdAsync(companyId);

                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Only allow Approved, Rejected, or Suspended
                if (status != CompanyStatusEnum.Approved && 
                    status != CompanyStatusEnum.Rejected && 
                    status != CompanyStatusEnum.Suspended)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Invalid company status. Only Approved, Rejected, or Suspended statuses are allowed."
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

                    case CompanyStatusEnum.Suspended:
                        // Keep existing approvedBy and rejection reason
                        await _companyRepository.UpdateAsync(company);

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company suspended."
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

        public async Task<ServiceResponse> CancelCompanyAsync()
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int currentUserId = int.Parse(userIdClaim);

                // Check if user has HR_Recruiter role
                if (user == null || !user.IsInRole("HR_Recruiter"))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only HR_Recruiter can cancel companies."
                    };
                }

                // Get company user to find associated company
                var companyUser = await _companyUserRepository.GetByUserIdAsync(currentUserId);
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

                // Check if current user is the creator of the company
                if (company.CreatedBy != currentUserId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You can only cancel companies you created."
                    };
                }

                // Only allow canceling if status is Pending
                if (company.CompanyStatus != CompanyStatusEnum.Pending)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "You can only cancel companies with Pending status."
                    };
                }

                // Update status to Canceled
                company.CompanyStatus = CompanyStatusEnum.Canceled;
                company.ApprovedBy = null;
                company.RejectReason = null;

                await _companyRepository.UpdateAsync(company);

                // Remove companyId from the user's CompanyUser record
                companyUser.CompanyId = null;
                companyUser.JoinStatus = JoinStatusEnum.NotApplied;
                await _companyUserRepository.UpdateAsync(companyUser);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company registration has been canceled successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error canceling company: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while canceling the company."
                };
            }
        }
    }
}
