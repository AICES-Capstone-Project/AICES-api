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

        public CampaignService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null)
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
                var campaigns = await campaignRepo.GetByCompanyIdWithFiltersAsync(companyUser.CompanyId.Value, page, pageSize, search, status, startDate, endDate);
                var total = await campaignRepo.GetTotalByCompanyIdWithFiltersAsync(companyUser.CompanyId.Value, search, status, startDate, endDate);

                var result = campaigns.Select(c => new CampaignResponse
                {
                    CampaignId = c.CampaignId,
                    CompanyId = c.CompanyId,
                    CompanyName = c.Company?.Name ?? "",
                    Title = c.Title,
                    Description = c.Description,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    JobIds = c.JobCampaigns?.Select(jc => jc.JobId).ToList() ?? new List<int>()
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
                        PageSize = pageSize
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
                    Data = new CampaignResponse
                    {
                        CampaignId = campaign.CampaignId,
                        CompanyId = campaign.CompanyId,
                        CompanyName = campaign.Company?.Name ?? "",
                        Title = campaign.Title,
                        Description = campaign.Description,
                        StartDate = campaign.StartDate,
                        EndDate = campaign.EndDate,
                        Status = campaign.Status,
                        CreatedAt = campaign.CreatedAt,
                        JobIds = campaign.JobCampaigns?.Select(jc => jc.JobId).ToList() ?? new List<int>()
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

        public async Task<ServiceResponse> GetMyCampaignsAsync()
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
                var campaigns = await campaignRepo.GetByCompanyIdAsync(companyUser.CompanyId.Value);
                var result = campaigns.Select(c => new CampaignResponse
                {
                    CampaignId = c.CampaignId,
                    CompanyId = c.CompanyId,
                    CompanyName = c.Company?.Name ?? "",
                    Title = c.Title,
                    Description = c.Description,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    JobIds = c.JobCampaigns?.Select(jc => jc.JobId).ToList() ?? new List<int>()
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

        public async Task<ServiceResponse> CreateAsync(CampaignRequest request)
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

                // Validate dates
                if (request.EndDate <= request.StartDate)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "End date must be after start date."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    var campaign = new Campaign
                    {
                        CompanyId = companyUser.CompanyId.Value,
                        Title = request.Title,
                        Description = request.Description,
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        Status = request.Status
                    };

                    var campaignRepo = _uow.GetRepository<ICampaignRepository>();
                    await campaignRepo.AddAsync(campaign);
                    await _uow.SaveChangesAsync(); // Get CampaignId

                    // Add job campaigns if provided
                    if (request.JobIds != null && request.JobIds.Any())
                    {
                        var jobRepo = _uow.GetRepository<IJobRepository>();
                        
                        foreach (var jobId in request.JobIds)
                        {
                            // Verify job belongs to the same company
                            var job = await jobRepo.GetJobByIdAsync(jobId);
                            if (job != null && job.CompanyId == companyUser.CompanyId.Value)
                            {
                                campaign.JobCampaigns.Add(new JobCampaign
                                {
                                    JobId = jobId,
                                    CampaignId = campaign.CampaignId
                                });
                            }
                        }
                    }

                    await _uow.CommitTransactionAsync();

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

        public async Task<ServiceResponse> UpdateAsync(int id, CampaignRequest request)
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

                // Validate dates
                if (request.EndDate <= request.StartDate)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "End date must be after start date."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    campaign.Title = request.Title ?? campaign.Title;
                    campaign.Description = request.Description ?? campaign.Description;
                    campaign.StartDate = request.StartDate;
                    campaign.EndDate = request.EndDate;
                    campaign.Status = request.Status;

                    // Update job campaigns
                    if (request.JobIds != null)
                    {
                        // Clear existing job campaigns
                        campaign.JobCampaigns.Clear();

                        // Add new job campaigns
                        if (request.JobIds.Any())
                        {
                            var jobRepo = _uow.GetRepository<IJobRepository>();
                            
                            foreach (var jobId in request.JobIds)
                            {
                                // Verify job belongs to the same company
                                var job = await jobRepo.GetJobByIdAsync(jobId);
                                if (job != null && job.CompanyId == companyUser.CompanyId.Value)
                                {
                                    campaign.JobCampaigns.Add(new JobCampaign
                                    {
                                        JobId = jobId,
                                        CampaignId = campaign.CampaignId
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

                // Validate request
                if (request.JobIds == null || !request.JobIds.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one job ID is required."
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

                    foreach (var jobId in request.JobIds)
                    {
                        // Skip if already in campaign
                        if (existingJobIds.Contains(jobId))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Verify job belongs to the same company
                        var job = await jobRepo.GetJobByIdAsync(jobId);
                        if (job != null && job.CompanyId == companyUser.CompanyId.Value && job.IsActive)
                        {
                            // Ensure JobCampaigns is initialized
                            if (campaign.JobCampaigns == null)
                            {
                                campaign.JobCampaigns = new List<JobCampaign>();
                            }
                            
                            campaign.JobCampaigns.Add(new JobCampaign
                            {
                                JobId = jobId,
                                CampaignId = campaign.CampaignId
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

        public async Task<ServiceResponse> UpdateStatusAsync(int id, UpdateCampaignStatusRequest request)
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

                await _uow.BeginTransactionAsync();
                try
                {
                    campaign.Status = request.Status;
                    campaignRepo.Update(campaign);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Campaign status updated successfully."
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
                Console.WriteLine($"Update campaign status error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the campaign status."
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
    }
}
