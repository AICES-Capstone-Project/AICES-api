using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Common;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CampaignService : ICampaignService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationService _notificationService;

        public CampaignService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor, INotificationService notificationService)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _notificationService = notificationService;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get user role to check if they can see Pending campaigns
                var userRepo = _uow.GetRepository<IUserRepository>();
                var currentUser = await userRepo.GetByIdAsync(userId);
                int roleId = currentUser?.RoleId ?? 0;

                // Filter Pending status: only HR Manager (roleId = 4) can see Pending campaigns
                CampaignStatusEnum? filteredStatus = status;
                if (status == CampaignStatusEnum.Pending && roleId != 4)
                {
                    // Non-HR Manager cannot see Pending campaigns, return empty result
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Campaigns retrieved successfully.",
                        Data = new PaginatedCampaignResponse
                        {
                            Campaigns = new List<CampaignResponse>(),
                            TotalPages = 0,
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalCount = 0
                        }
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaigns = await campaignRepo.GetByCompanyIdWithFiltersAsync(companyUser.CompanyId.Value, page, pageSize, search, filteredStatus, startDate, endDate);
                var total = await campaignRepo.GetTotalByCompanyIdWithFiltersAsync(companyUser.CompanyId.Value, search, filteredStatus, startDate, endDate);

                var result = campaigns.Select(c => new CampaignResponse
                {
                    CampaignId = c.CampaignId,
                    Title = c.Title,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    TotalJobs = c.JobCampaigns?.Count ?? 0,
                    TotalHired = c.TotalHired.ToString(),
                    TotalTarget = c.TotalTarget.ToString()
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Campaigns retrieved successfully.",
                    Data = new PaginatedCampaignResponse
                    {
                        Campaigns = result,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalCount = total
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get campaigns error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving campaigns."
                };
            }
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetByIdAsync(id);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this campaign."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Campaign retrieved successfully.",
                    Data = new CampaignDetailResponse
                    {
                        CampaignId = campaign.CampaignId,
                        CompanyId = campaign.CompanyId,
                        CompanyName = campaign.Company?.Name ?? "",
                        CreatorName = campaign.Creator?.Profile?.FullName ?? campaign.Creator?.Email,
                        Title = campaign.Title,
                        Description = campaign.Description,
                        StartDate = campaign.StartDate,
                        EndDate = campaign.EndDate,
                        Status = campaign.Status,
                        CreatedAt = campaign.CreatedAt,
                        TotalHired = campaign.TotalHired.ToString(),
                        TotalTarget = campaign.TotalTarget.ToString(),
                        Jobs = campaign.JobCampaigns?.Select(jc => new JobCampaignInfoResponse
                        {
                            JobId = jc.JobId,
                            JobTitle = jc.Job?.Title,
                            TargetQuantity = jc.TargetQuantity,
                            CurrentHired = jc.CurrentHired
                        }).ToList() ?? new List<JobCampaignInfoResponse>()
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the campaign."
                };
            }
        }

        public async Task<ServiceResponse> GetMyCampaignsAsync(int page = 1, int pageSize = 10, string? search = null, CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaigns = await campaignRepo.GetByCompanyIdAsync(companyUser.CompanyId.Value);
                var result = campaigns.Select(c => new CampaignResponse
                {
                    CampaignId = c.CampaignId,
                    Title = c.Title,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    TotalJobs = c.JobCampaigns?.Count ?? 0,
                    TotalHired = c.TotalHired.ToString(),
                    TotalTarget = c.TotalTarget.ToString()
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "My campaigns retrieved successfully.",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get my campaigns error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving your campaigns."
                };
            }
        }

        public async Task<ServiceResponse> GetMyCampaignsByIdAsync(int id)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                
                var campaign = await campaignRepo.GetByIdAsync(id);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this campaign."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Campaign retrieved successfully.",
                    Data = new CampaignDetailResponse
                    {
                        CampaignId = campaign.CampaignId,
                        CompanyId = campaign.CompanyId,
                        CompanyName = campaign.Company?.Name ?? "",
                        CreatorName = campaign.Creator?.Profile?.FullName ?? campaign.Creator?.Email,
                        Title = campaign.Title,
                        Description = campaign.Description,
                        StartDate = campaign.StartDate,
                        EndDate = campaign.EndDate,
                        Status = campaign.Status,
                        CreatedAt = campaign.CreatedAt,
                        TotalHired = campaign.TotalHired.ToString(),
                        TotalTarget = campaign.TotalTarget.ToString(),
                        Jobs = campaign.JobCampaigns?.Select(jc => new JobCampaignInfoResponse
                        {
                            JobId = jc.JobId,
                            JobTitle = jc.Job?.Title,
                            TargetQuantity = jc.TargetQuantity,
                            CurrentHired = jc.CurrentHired
                        }).ToList() ?? new List<JobCampaignInfoResponse>()
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get my campaign by id error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the campaign."
                };
            }
        }

        public async Task<ServiceResponse> GetCampaignJobsAsync(int campaignId, int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetByIdAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this campaign."
                    };
                }

                var jobCampaignRepo = _uow.GetRepository<ICampaignRepository>();
                var (jobCampaigns, totalCount) = await jobCampaignRepo.GetActiveJobsByCampaignIdAsync(campaignId, page, pageSize, search);

                var paginatedJobs = jobCampaigns.Select(jc => new JobCampaignInfoResponse
                {
                    JobId = jc.JobId,
                    JobTitle = jc.Job?.Title,
                    TargetQuantity = jc.TargetQuantity,
                    CurrentHired = jc.CurrentHired
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Campaign jobs retrieved successfully.",
                    Data = new PaginatedJobCampaignResponse
                    {
                        Jobs = paginatedJobs,
                        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get campaign jobs error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving campaign jobs."
                };
            }
        }

        public async Task<ServiceResponse> CreateAsync(CreateCampaignRequest request)
        {
            try
            {
                // Get current user
                var userClaims = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = userClaims != null ? ClaimUtils.GetUserIdClaim(userClaims) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);
                
                // Get user to check roleId
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var emailClaim = userClaims != null ? ClaimUtils.GetEmailClaim(userClaims) : null;
                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Email claim not found in token."
                    };
                }
                var user = await authRepo.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not found."
                    };
                }

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate dates
                if (request.EndDate <= request.StartDate.AddDays(1))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "End date must be at least one day after start date."
                    };
                }

                // Check if title already exists in company
                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                var titleExists = await campaignRepo.ExistsByTitleAndCompanyIdAsync(request.Title, companyUser.CompanyId.Value);
                if (titleExists)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "A campaign with this title already exists in your company."
                    };
                }

                // Determine CampaignStatus based on user roleId
                // roleId = 5 (HR_Recruiter) → Pending
                // roleId = 4 (HR_Manager) → Published
                var campaignStatus = user.RoleId == 5 
                    ? CampaignStatusEnum.Pending 
                    : (user.RoleId == 4 
                        ? CampaignStatusEnum.Published
                        : CampaignStatusEnum.Pending);

                await _uow.BeginTransactionAsync();
                try
                {
                    var campaign = new Campaign
                    {
                        CompanyId = companyUser.CompanyId.Value,
                        CreatedBy = userId,
                        Title = request.Title,
                        Description = request.Description,
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        Status = campaignStatus
                    };

                    await campaignRepo.AddAsync(campaign);
                    await _uow.SaveChangesAsync(); // Get CampaignId

                    // Add job campaigns if provided
                    if (request.Jobs != null && request.Jobs.Any())
                    {
                        var jobRepo = _uow.GetRepository<IJobRepository>();
                        
                        foreach (var jobWithTarget in request.Jobs)
                        {
                            // Verify job belongs to the same company
                            var job = await jobRepo.GetJobByIdAsync(jobWithTarget.JobId);
                            if (job != null && job.CompanyId == companyUser.CompanyId.Value)
                            {
                                campaign.JobCampaigns.Add(new JobCampaign
                                {
                                    JobId = jobWithTarget.JobId,
                                    CampaignId = campaign.CampaignId,
                                    TargetQuantity = jobWithTarget.TargetQuantity,
                                    CurrentHired = 0
                                });
                            }
                        }
                    }

                    await _uow.CommitTransactionAsync();

                    // 🔔 If created by HR_Recruiter (roleId = 5), notify all HR_Managers (roleId = 4) in the same company for approval
                    if (user.RoleId == 5)
                    {
                        var hrManagers = await companyUserRepo.GetHrManagerUsersByCompanyIdAsync(companyUser.CompanyId.Value);

                        foreach (var manager in hrManagers)
                        {
                            await _notificationService.CreateAsync(
                                userId: manager.UserId,
                                type: NotificationTypeEnum.Campaign,
                                message: "New campaign pending approval",
                                detail: $"HR Recruiter '{user.Profile?.FullName ?? user.Email}' created a new campaign '{campaign.Title}' that is awaiting your approval."
                            );
                        }
                    }

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Campaign created successfully."
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
                Console.WriteLine($"Create campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the campaign."
                };
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, UpdateCampaignRequest request)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetForUpdateAsync(id);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to update this campaign."
                    };
                }

                // Only allow updating campaigns that are Published
                // Exception: Expired campaigns can be updated if extending EndDate (which auto-publishes)
                bool isExpiredExtension = campaign.Status == CampaignStatusEnum.Expired && request.EndDate.HasValue && request.EndDate.Value > DateTime.UtcNow;
                
                if (campaign.Status != CampaignStatusEnum.Published && !isExpiredExtension)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Campaigns can only be updated when they are Published. Current status: {campaign.Status}"
                    };
                }

                // Check if title already exists in company (excluding current campaign)
                if (request.Title != null && request.Title != campaign.Title)
                {
                    var titleExists = await campaignRepo.ExistsByTitleAndCompanyIdAsync(request.Title, companyUser.CompanyId.Value, id);
                    if (titleExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Duplicated,
                            Message = "A campaign with this title already exists in your company."
                        };
                    }
                }

                // Validate endDate against existing startDate
                if (request.EndDate.HasValue && request.EndDate.Value <= campaign.StartDate.AddDays(1))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "End date must be at least one day after the existing start date."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Only update fields that are provided (PATCH behavior)
                    if (request.Title != null)
                    {
                        campaign.Title = request.Title;
                    }

                    if (request.Description != null)
                    {
                        campaign.Description = request.Description;
                    }

                    if (request.EndDate.HasValue)
                    {
                        // Auto-change status from Expired to Published if endDate is extended to a future date
                        if (campaign.Status == CampaignStatusEnum.Expired && request.EndDate.Value > DateTime.UtcNow)
                        {
                            campaign.Status = CampaignStatusEnum.Published;
                        }
                        campaign.EndDate = request.EndDate.Value;
                    }

                    // Update job campaigns only if provided
                    if (request.Jobs != null)
                    {
                        var jobRepo = _uow.GetRepository<IJobRepository>();
                        
                        // Get existing job campaigns to preserve CurrentHired
                        var existingJobCampaigns = campaign.JobCampaigns?.ToList() ?? new List<JobCampaign>();
                        var existingJobCampaignsDict = existingJobCampaigns.ToDictionary(jc => jc.JobId);
                        
                        // Clear existing job campaigns
                        if (campaign.JobCampaigns != null)
                        {
                            campaign.JobCampaigns.Clear();
                        }
                        else
                        {
                            campaign.JobCampaigns = new List<JobCampaign>();
                        }

                        // Add new job campaigns
                        if (request.Jobs.Any())
                        {
                            foreach (var jobWithTarget in request.Jobs)
                            {
                                // Verify job belongs to the same company
                                var job = await jobRepo.GetJobByIdAsync(jobWithTarget.JobId);
                                if (job != null && job.CompanyId == companyUser.CompanyId.Value)
                                {
                                    // Preserve CurrentHired if job was already in campaign, otherwise set to 0
                                    var existingCurrentHired = existingJobCampaignsDict.ContainsKey(jobWithTarget.JobId)
                                        ? existingJobCampaignsDict[jobWithTarget.JobId].CurrentHired
                                        : 0;
                                    
                                    campaign.JobCampaigns.Add(new JobCampaign
                                    {
                                        JobId = jobWithTarget.JobId,
                                        CampaignId = campaign.CampaignId,
                                        TargetQuantity = jobWithTarget.TargetQuantity,
                                        CurrentHired = existingCurrentHired
                                    });
                                }
                            }
                        }
                    }

                    campaignRepo.Update(campaign);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Campaign updated successfully."
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
                Console.WriteLine($"Update campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the campaign."
                };
            }
        }

        public async Task<ServiceResponse> AddJobsToCampaignAsync(int campaignId, AddJobsToCampaignRequest request)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetForUpdateAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to modify this campaign."
                    };
                }

                // Only allow adding jobs to Published campaigns
                if (campaign.Status != CampaignStatusEnum.Published)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Jobs can only be added to Published campaigns."
                    };
                }

                // Validate request
                if (request.Jobs == null || !request.Jobs.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one job is required."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    var jobRepo = _uow.GetRepository<IJobRepository>();
                    
                    // Get existing job IDs from the campaign
                    var existingJobIds = campaign.JobCampaigns != null && campaign.JobCampaigns.Any()
                        ? campaign.JobCampaigns.Select(jc => jc.JobId).ToList()
                        : new List<int>();
                    var addedCount = 0;
                    var skippedCount = 0;

                    foreach (var jobWithTarget in request.Jobs)
                    {
                        // Skip if already in campaign
                        if (existingJobIds.Contains(jobWithTarget.JobId))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Verify job belongs to the same company
                        var job = await jobRepo.GetJobByIdAsync(jobWithTarget.JobId);
                        if (job != null && job.CompanyId == companyUser.CompanyId.Value && job.IsActive)
                        {
                            // Ensure JobCampaigns is initialized
                            if (campaign.JobCampaigns == null)
                            {
                                campaign.JobCampaigns = new List<JobCampaign>();
                            }
                            
                            campaign.JobCampaigns.Add(new JobCampaign
                            {
                                JobId = jobWithTarget.JobId,
                                CampaignId = campaign.CampaignId,
                                TargetQuantity = jobWithTarget.TargetQuantity,
                                CurrentHired = 0
                            });
                            addedCount++;
                        }
                    }

                    if (addedCount == 0)
                    {
                        await _uow.RollbackTransactionAsync();
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = skippedCount > 0 
                                ? "All jobs are already in the campaign or invalid." 
                                : "No valid jobs found to add."
                        };
                    }

                    campaignRepo.Update(campaign);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = $"Successfully added {addedCount} job(s) to campaign." + 
                                  (skippedCount > 0 ? $" {skippedCount} job(s) were already in the campaign." : "")
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
                Console.WriteLine($"Add jobs to campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while adding jobs to the campaign."
                };
            }
        }

        public async Task<ServiceResponse> RemoveJobsFromCampaignAsync(int campaignId, RemoveJobsFromCampaignRequest request)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetForUpdateAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to modify this campaign."
                    };
                }

                // Only allow removing jobs from Published campaigns
                if (campaign.Status != CampaignStatusEnum.Published)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Jobs can only be removed from Published campaigns."
                    };
                }

                // Validate request
                if (request.JobIds == null || !request.JobIds.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one job is required."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Ensure JobCampaigns is initialized
                    if (campaign.JobCampaigns == null || !campaign.JobCampaigns.Any())
                    {
                        await _uow.RollbackTransactionAsync();
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "Campaign has no jobs to remove."
                        };
                    }

                    var jobsToRemove = campaign.JobCampaigns
                        .Where(jc => request.JobIds.Contains(jc.JobId))
                        .ToList();

                    if (!jobsToRemove.Any())
                    {
                        await _uow.RollbackTransactionAsync();
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "None of the specified jobs are in this campaign."
                        };
                    }

                    var removedCount = jobsToRemove.Count;
                    foreach (var jobCampaign in jobsToRemove)
                    {
                        campaign.JobCampaigns.Remove(jobCampaign);
                    }

                    campaignRepo.Update(campaign);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = $"Successfully removed {removedCount} job(s) from campaign."
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
                Console.WriteLine($"Remove jobs from campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while removing jobs from the campaign."
                };
            }
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            try
            {
                // Get current user
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

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
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                await MarkExpiredCampaignsAsync(campaignRepo, companyUser.CompanyId.Value);
                var campaign = await campaignRepo.GetForUpdateWithJobsAsync(id);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Prevent deleting campaign if there are any jobs associated with this campaign
                if (campaign.JobCampaigns != null && campaign.JobCampaigns.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Cannot delete this campaign because there are existing jobs associated with it."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to delete this campaign."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    campaign.IsActive = false;
                    campaignRepo.Update(campaign);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Campaign deleted successfully."
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
                Console.WriteLine($"Delete campaign error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting the campaign."
                };
            }
        }

        public async Task<ServiceResponse> UpdateStatusAsync(int id, UpdateCampaignStatusRequest? request = null)
        {
            try
            {
                // Get current user
                var userClaims = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = userClaims != null ? ClaimUtils.GetUserIdClaim(userClaims) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);
                
                // Check if user is HR_Manager (roleId = 4)
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var emailClaim = userClaims != null ? ClaimUtils.GetEmailClaim(userClaims) : null;
                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Email claim not found in token."
                    };
                }
                var userEntity = await authRepo.GetByEmailAsync(emailClaim);
                if (userEntity == null || userEntity.RoleId != 4)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only HR_Manager can update campaign status."
                    };
                }

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                var campaign = await campaignRepo.GetForUpdateWithAllStatusesAsync(id);
                
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                // Verify campaign belongs to user's company
                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to update this campaign."
                    };
                }

                // Validate status transition
                var currentStatus = campaign.Status;
                var newStatus = request?.Status;

                if (!newStatus.HasValue)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Status is required."
                    };
                }

                // Validate allowed transitions:
                // - Pending -> Published/Rejected
                // - Published <-> Paused
                bool isValidTransition = false;
                string transitionMessage = "";

                if (currentStatus == CampaignStatusEnum.Pending)
                {
                    if (newStatus.Value == CampaignStatusEnum.Published || newStatus.Value == CampaignStatusEnum.Rejected)
                    {
                        isValidTransition = true;
                    }
                    else
                    {
                        transitionMessage = "Pending campaigns can only be changed to Published or Rejected.";
                    }
                }
                else if (currentStatus == CampaignStatusEnum.Published)
                {
                    if (newStatus.Value == CampaignStatusEnum.Paused)
                    {
                        isValidTransition = true;
                    }
                    else
                    {
                        transitionMessage = "Published campaigns can only be changed to Paused.";
                    }
                }
                else if (currentStatus == CampaignStatusEnum.Paused)
                {
                    if (newStatus.Value == CampaignStatusEnum.Published)
                    {
                        isValidTransition = true;
                    }
                    else
                    {
                        transitionMessage = "Paused campaigns can only be changed to Published.";
                    }
                }
                else
                {
                    transitionMessage = $"Cannot change status from {currentStatus} to {newStatus.Value}.";
                }

                if (!isValidTransition)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = transitionMessage
                    };
                }

                campaign.Status = newStatus.Value;
                campaignRepo.Update(campaign);
                await _uow.SaveChangesAsync();

                // 🔔 Notify HR_Recruiter (roleId = 5) if they created the campaign
                if (campaign.CreatedBy.HasValue)
                {
                    var userRepo = _uow.GetRepository<IUserRepository>();
                    var creator = await userRepo.GetByIdAsync(campaign.CreatedBy.Value);
                    
                    if (creator != null && creator.RoleId == 5)
                    {
                        string action = newStatus.Value == CampaignStatusEnum.Published ? "approved" : 
                                        newStatus.Value == CampaignStatusEnum.Rejected ? "rejected" : 
                                        newStatus.Value == CampaignStatusEnum.Paused ? "paused" :
                                        newStatus.Value.ToString().ToLower();
                                        
                        await _notificationService.CreateAsync(
                            userId: creator.UserId,
                            type: NotificationTypeEnum.Campaign,
                            message: $"Campaign {action}",
                            detail: $"Your campaign '{campaign.Title}' has been {action} by the HR Manager."
                        );
                    }
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Campaign status updated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update campaign status error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the campaign status."
                };
            }
        }

        private async Task MarkExpiredCampaignsAsync(ICampaignRepository campaignRepo, int companyId)
        {
            await campaignRepo.MarkExpiredCampaignsAsync(DateTime.UtcNow, companyId);
        }
    }
}

