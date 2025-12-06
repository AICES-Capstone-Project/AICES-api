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
using DataAccessLayer.UnitOfWork;
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
        private readonly IUnitOfWork _uow;
        private readonly ICompanyDocumentService _companyDocumentService;
        private readonly Common.CloudinaryHelper _cloudinaryHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationService _notificationService;

        public CompanyService(
            IUnitOfWork uow,
            ICompanyDocumentService companyDocumentService,
            Common.CloudinaryHelper cloudinaryHelper,
            IHttpContextAccessor httpContextAccessor,
            INotificationService notificationService)
        {
            _uow = uow;
            _companyDocumentService = companyDocumentService;
            _cloudinaryHelper = cloudinaryHelper;
            _httpContextAccessor = httpContextAccessor;
            _notificationService = notificationService;
        }

        // Get public companies (active and approved only)
        public async Task<ServiceResponse> GetPublicAsync()
        {
            try
            {
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var companies = await companyRepo.GetPublicCompaniesAsync();

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
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetPublicByIdAsync(id);

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
        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CompanyStatusEnum? status = null)
        {
            try
            {
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var companies = await companyRepo.GetCompaniesWithCreatorAsync(page, pageSize, search, status);
                var total = await companyRepo.GetTotalCompaniesAsync(search, status);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Companies retrieved successfully.",
                    Data = new PaginatedCompanyResponse
                    {
                        Companies = companies,
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
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetByIdWithCreatorAsync(id);

                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company retrieved successfully.",
                    Data = company
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

                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                
                // ✅ Kiểm tra trùng tên công ty
                if (await companyRepo.ExistsByNameAsync(request.Name))
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

                await _uow.BeginTransactionAsync();
                try
                {
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
                        RejectReason = null
                    };

                    // Save company first to get CompanyId
                    await companyRepo.AddAsync(company);
                    await _uow.SaveChangesAsync(); // Get CompanyId

                    // ✅ Upload documents (nếu có) using CompanyDocumentService
                    if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
                    {
                        await _companyDocumentService.UploadAndSaveDocumentsAsync(
                            company.CompanyId,
                            request.DocumentFiles,
                            request.DocumentTypes);
                    }

                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company created successfully.",
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
                var (userResult, companyUser, company, managerName) = await GetSelfCompanyContextAsync(
                    requireApprovedJoinStatus: true,
                    requireRejectedCompanyStatus: false,
                    requireNotAppliedJoinStatusForRejected: false);

                if (userResult != null)
                {
                    return userResult;
                }

                var companyResponse = BuildSelfCompanyResponse(company, managerName);

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

        // Get self company when status is Rejected (for HR recruiter after rejection)
        public async Task<ServiceResponse> GetRejectedSelfCompanyAsync()
        {
            try
            {
                var (userResult, companyUser, company, managerName) = await GetSelfCompanyContextAsync(
                    requireApprovedJoinStatus: false,
                    requireRejectedCompanyStatus: true,
                    requireNotAppliedJoinStatusForRejected: true);

                if (userResult != null)
                {
                    return userResult;
                }

                var companyResponse = BuildSelfCompanyResponse(company, managerName);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Rejected company profile retrieved successfully.",
                    Data = companyResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get rejected self company error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving your rejected company."
                };
            }
        }

        /// <summary>
        /// Shared context loader for self-company endpoints.
        /// </summary>
        private async Task<(ServiceResponse? userResult, CompanyUser companyUser, Company company, string? managerName)>
            GetSelfCompanyContextAsync(bool requireApprovedJoinStatus, bool requireRejectedCompanyStatus, bool requireNotAppliedJoinStatusForRejected)
        {
            // Get current user ID from claims
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                }, null!, null!, null);
            }

            int userId = int.Parse(userIdClaim);

            // Get company user to find associated company
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyRepo = _uow.GetRepository<ICompanyRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company user not found."
                }, null!, null!, null);
            }

            if (companyUser.CompanyId == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "You are not associated with any company."
                }, null!, null!, null);
            }

            if (requireApprovedJoinStatus && companyUser.JoinStatus != JoinStatusEnum.Approved)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "Only approved or invited members can access company information."
                }, null!, null!, null);
            }

            var company = await companyRepo.GetByIdAsync(companyUser.CompanyId.Value);

            if (company == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                }, null!, null!, null);
            }

            if (requireRejectedCompanyStatus && company.CompanyStatus != CompanyStatusEnum.Rejected)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "This endpoint is only available when your company status is Rejected."
                }, null!, null!, null);
            }

            if (requireNotAppliedJoinStatusForRejected && companyUser.JoinStatus != JoinStatusEnum.NotApplied)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "This endpoint is only available when your join status is NotApplied."
                }, null!, null!, null);
            }

            // Get manager name (HR_Manager role)
            string? managerName = null;
            var members = await companyUserRepo.GetApprovedAndInvitedMembersByCompanyIdAsync(companyUser.CompanyId.Value);
            var manager = members.FirstOrDefault(m =>
                m.User?.Role?.RoleName == "HR_Manager" || m.User?.RoleId == 4);
            managerName = manager?.User?.Profile?.FullName;

            return (null, companyUser, company, managerName);
        }

        private static SelfCompanyResponse BuildSelfCompanyResponse(Company company, string? managerName)
        {
            return new SelfCompanyResponse
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
                ManagerName = managerName,
                Documents = company.CompanyDocuments?.Select(d => new CompanyDocumentResponse
                {
                    DocumentType = d.DocumentType ?? "",
                    FileUrl = d.FileUrl ?? ""
                }).ToList() ?? new List<CompanyDocumentResponse>()
            };
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

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                
                // ✅ Check if user already belongs to a different company BEFORE creating
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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
                if (await companyRepo.ExistsByNameAsync(request.Name))
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

                await _uow.BeginTransactionAsync();
                try
                {
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
                        RejectReason = null
                    };

                    // Save company first to get CompanyId
                    await companyRepo.AddAsync(company);
                    await _uow.SaveChangesAsync(); // Get CompanyId

                    // ✅ Upload documents (nếu có) using CompanyDocumentService
                    if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
                    {
                        await _companyDocumentService.UploadAndSaveDocumentsAsync(
                            company.CompanyId,
                            request.DocumentFiles,
                            request.DocumentTypes);
                    }

                    // Associate user with the newly created company
                    companyUser.CompanyId = company.CompanyId;
                    await companyUserRepo.UpdateAsync(companyUser);
                    
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company created successfully. Waiting for admin approval.",
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetForUpdateAsync(companyUser.CompanyId.Value);
                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Only allow update if company status is Rejected AND companyUser status is NotApplied
                if (company.CompanyStatus != CompanyStatusEnum.Rejected)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Company can only be updated when company status is Rejected."
                    };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.NotApplied)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Company can only be updated when your join status is NotApplied."
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

                await _uow.BeginTransactionAsync();
                try
                {
                    await companyRepo.UpdateAsync(company);

                    // Upload new documents if provided
                    if (request.DocumentFiles != null && request.DocumentFiles.Count > 0)
                    {
                        await _companyDocumentService.UploadAndSaveDocumentsAsync(
                            company.CompanyId,
                            request.DocumentFiles,
                            request.DocumentTypes);
                    }

                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company updated successfully. Status set to Pending for admin review."
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
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetForUpdateAsync(id);
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

                await _uow.BeginTransactionAsync();
                try
                {
                    await companyRepo.UpdateAsync(company);
                    await _uow.SaveChangesAsync();

                    // Upload new documents if provided
                    if (request.DocumentFiles != null && request.DocumentFiles.Any())
                    {
                        await _companyDocumentService.UploadAndSaveDocumentsAsync(
                            company.CompanyId,
                            request.DocumentFiles,
                            request.DocumentTypes);
                    }

                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company updated successfully. Status set to Pending for admin review."
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
        public async Task<ServiceResponse> UpdateCompanyProfileAsync(CompanyProfileUpdateRequest request)
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only approved or invited members can update company profile."
                    };
                }

                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetForUpdateAsync(companyUser.CompanyId.Value);
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

                await _uow.BeginTransactionAsync();
                try
                {
                    await companyRepo.UpdateAsync(company);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Company profile updated successfully."
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
            var companyRepo = _uow.GetRepository<ICompanyRepository>();
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var userRepo = _uow.GetRepository<IUserRepository>();
            
            var company = await companyRepo.GetForUpdateAsync(id);
            if (company == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company not found."
                };
            }

            // Get all CompanyUsers for this company (read-only list to get IDs)
            var companyUsersList = await companyUserRepo.GetMembersByCompanyIdAsync(id);
            
            await _uow.BeginTransactionAsync();
            try
            {
                // Soft delete company
                company.IsActive = false;
                await companyRepo.UpdateAsync(company);

                // Process each CompanyUser
                foreach (var companyUserInfo in companyUsersList)
                {
                    // Get CompanyUser with tracking enabled for update
                    var companyUser = await companyUserRepo.GetByComUserIdAsync(companyUserInfo.ComUserId);
                    if (companyUser == null || companyUser.User == null)
                        continue;

                    // Remove companyId from CompanyUser
                    companyUser.CompanyId = null;
                    await companyUserRepo.UpdateAsync(companyUser);

                    // If user is HR_Manager (roleId = 4), change to HR_Recruiter (roleId = 5)
                    if (companyUser.User.RoleId == 4)
                    {
                        // Get User with tracking enabled to ensure proper update
                        var user = await userRepo.GetForUpdateAsync(companyUser.UserId);
                        if (user != null)
                        {
                            user.RoleId = 5;
                            await userRepo.UpdateAsync(user);
                        }
                    }

                    // Send notification to user
                    try
                    {
                        await _notificationService.CreateAsync(
                            userId: companyUser.UserId,
                            type: NotificationTypeEnum.Company,
                            message: $"Company '{company.Name}' has been deleted",
                            detail: $"Your company '{company.Name}' has been deleted. Your company association has been removed."
                        );
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the transaction if notification fails
                        Console.WriteLine($"Error sending notification to user {companyUser.UserId}: {ex.Message}");
                    }
                }

                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company deleted successfully and all associated users have been notified."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
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

                // Only System_Admin or System_Manager can update company status
                if (user == null || (!user.IsInRole("System_Admin") && !user.IsInRole("System_Manager")))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only System_Admin and System_Manager can update company status."
                    };
                }

                // Get company
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetForUpdateAsync(companyId);
                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Get current status
                var currentStatus = company.CompanyStatus;

                // Validate status transition based on current status
                bool isValidTransition = false;
                string transitionErrorMessage = "";

                switch (currentStatus)
                {
                    case CompanyStatusEnum.Pending:
                        // Pending -> Rejected, Approved, Suspended
                        if (status == CompanyStatusEnum.Rejected || 
                            status == CompanyStatusEnum.Approved || 
                            status == CompanyStatusEnum.Suspended)
                        {
                            isValidTransition = true;
                        }
                        else
                        {
                            transitionErrorMessage = "From Pending status, only Rejected, Approved, or Suspended statuses are allowed.";
                        }
                        break;

                    case CompanyStatusEnum.Approved:
                        // Approved -> Suspended
                        if (status == CompanyStatusEnum.Suspended)
                        {
                            isValidTransition = true;
                        }
                        else
                        {
                            transitionErrorMessage = "From Approved status, only Suspended status is allowed.";
                        }
                        break;

                    case CompanyStatusEnum.Rejected:
                        // Rejected -> Approved, Suspended
                        if (status == CompanyStatusEnum.Approved || 
                            status == CompanyStatusEnum.Suspended)
                        {
                            isValidTransition = true;
                        }
                        else
                        {
                            transitionErrorMessage = "From Rejected status, only Approved or Suspended statuses are allowed.";
                        }
                        break;

                    case CompanyStatusEnum.Suspended:
                        // Suspended -> Approved
                        if (status == CompanyStatusEnum.Approved)
                        {
                            isValidTransition = true;
                        }
                        else
                        {
                            transitionErrorMessage = "From Suspended status, only Approved status is allowed.";
                        }
                        break;

                    case CompanyStatusEnum.Canceled:
                        transitionErrorMessage = "Cannot change status from Canceled status.";
                        break;

                    default:
                        transitionErrorMessage = $"Invalid current status: {currentStatus}.";
                        break;
                }

                if (!isValidTransition)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = transitionErrorMessage
                    };
                }

                // Check if status is actually changing
                if (currentStatus == status)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Company is already in the requested status."
                    };
                }

                // Update company status
                company.CompanyStatus = status;

                // Get company creator (Recruiter)
                var creator = company.CompanyUsers?.FirstOrDefault();
                int? creatorUserId = creator?.UserId;

                // Handle each status
                switch (status)
                {
                    case CompanyStatusEnum.Approved:
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            company.ApprovedBy = currentUserId;
                            company.RejectReason = null;
                            await companyRepo.UpdateAsync(company);
                            await _uow.SaveChangesAsync();

                            // Promote the recruiter to HR_Manager
                            bool updated = await companyRepo.UpdateUserRoleByCompanyAsync(companyId, "HR_Manager");
                            if (!updated)
                            {
                                await _uow.RollbackTransactionAsync();
                                return new ServiceResponse
                                {
                                    Status = SRStatus.Error,
                                    Message = "Company approved, but failed to promote the recruiter to HR_Manager."
                                };
                            }
                            
                            await _uow.CommitTransactionAsync();

                            // Send notification to recruiter
                            if (creatorUserId.HasValue)
                            {
                                await _notificationService.CreateAsync(
                                    userId: creatorUserId.Value,
                                    type: NotificationTypeEnum.Company,
                                    message: $"Your company has been approved",
                                    detail: $"Congratulations! Your company '{company.Name}' has been approved by the admin."
                                );
                            }

                            return new ServiceResponse
                            {
                                Status = SRStatus.Success,
                                Message = "Company approved and notification sent."
                            };
                        }
                        catch
                        {
                            await _uow.RollbackTransactionAsync();
                            throw;
                        }

                    case CompanyStatusEnum.Rejected:
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            company.RejectReason = rejectionReason;
                            company.ApprovedBy = null;
                            await companyRepo.UpdateAsync(company);

                            // Remove companyId from creator's CompanyUser
                            if (creatorUserId.HasValue)
                            {
                                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                                // Get CompanyUser by UserId (read-only to get ComUserId)
                                var creatorCompanyUserInfo = await companyUserRepo.GetByUserIdAsync(creatorUserId.Value);
                                
                                if (creatorCompanyUserInfo != null && creatorCompanyUserInfo.CompanyId == companyId)
                                {
                                    // Get CompanyUser with tracking enabled for update
                                    var creatorCompanyUser = await companyUserRepo.GetByComUserIdAsync(creatorCompanyUserInfo.ComUserId);
                                    if (creatorCompanyUser != null)
                                    {
                                        creatorCompanyUser.CompanyId = null;
                                        creatorCompanyUser.JoinStatus = JoinStatusEnum.NotApplied;
                                        await companyUserRepo.UpdateAsync(creatorCompanyUser);
                                    }
                                }
                            }

                            await _uow.CommitTransactionAsync();
                        }
                        catch
                        {
                            await _uow.RollbackTransactionAsync();
                            throw;
                        }

                        // Send rejection notification
                        if (creatorUserId.HasValue)
                        {
                            await _notificationService.CreateAsync(
                                userId: creatorUserId.Value,
                                type: NotificationTypeEnum.Company,
                                message: $"Your company has been rejected",
                                detail: $"Reason: {rejectionReason ?? "No reason provided."}"
                            );
                        }

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company rejected and notification sent."
                        };

                    case CompanyStatusEnum.Suspended:
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            await companyRepo.UpdateAsync(company);
                            await _uow.CommitTransactionAsync();
                        }
                        catch
                        {
                            await _uow.RollbackTransactionAsync();
                            throw;
                        }

                        // Send suspension notification
                        if (creatorUserId.HasValue)
                        {
                            await _notificationService.CreateAsync(
                                userId: creatorUserId.Value,
                                type: NotificationTypeEnum.Company,
                                message: $"Your company has been suspended",
                                detail: $"Your company has been temporarily suspended. Please contact support for more details."
                            );  
                        }

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Company suspended and notification sent."
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(currentUserId);
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

                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var company = await companyRepo.GetForUpdateAsync(companyUser.CompanyId.Value);

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

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update status to Canceled
                    company.CompanyStatus = CompanyStatusEnum.Canceled;
                    company.ApprovedBy = null;
                    company.RejectReason = null;

                    await companyRepo.UpdateAsync(company);

                    // Remove companyId from the user's CompanyUser record
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
