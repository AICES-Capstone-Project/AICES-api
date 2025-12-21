using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BusinessObjectLayer.Services
{
    public class ComparisonService : IComparisonService
    {
        private readonly IUnitOfWork _uow;
        private readonly RedisHelper _redisHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IResumeApplicationRepository _resumeApplicationRepo;
        private readonly IComparisonLimitService _comparisonLimitService;

        public ComparisonService(
            IUnitOfWork uow,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor,
            IResumeApplicationRepository resumeApplicationRepo,
            IComparisonLimitService comparisonLimitService)
        {
            _uow = uow;
            _redisHelper = redisHelper;
            _httpContextAccessor = httpContextAccessor;
            _resumeApplicationRepo = resumeApplicationRepo;
            _comparisonLimitService = comparisonLimitService;
        }

        public async Task<ServiceResponse> CompareApplicationsAsync(CompareApplicationsRequest request)
        {
            if (request == null || request.ApplicationIds == null || request.ApplicationIds.Count < 2)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "At least 2 applications are required for comparison."
                };
            }

            if (request.ApplicationIds.Count > 3)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Maximum 3 applications can be compared at once."
                };
            }

            try
            {
                // Get current user and company
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
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var comparisonRepo = _uow.GetRepository<IComparisonRepository>();
                var applicationComparisonRepo = _uow.GetRepository<IApplicationComparisonRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }
                int companyId = companyUser.CompanyId.Value;

                // Validate job exists and belongs to company
                var job = await jobRepo.GetJobByIdAsync(request.JobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to access this job."
                    };
                }

                // Check for duplicate comparison
                var isDuplicate = await comparisonRepo.IsDuplicateComparisonAsync(companyId, request.ApplicationIds);
                if (isDuplicate)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "A comparison for these applications already exists for your company."
                    };
                }

                // ✅ Check comparison limit (early check for fast fail)
                var earlyLimitCheck = await _comparisonLimitService.CheckComparisonLimitAsync(companyId);
                if (earlyLimitCheck.Status != SRStatus.Success)
                {
                    return earlyLimitCheck;
                }

                // Get all applications and validate they belong to this job/campaign
                var applications = await _resumeApplicationRepo.GetByJobIdAndApplicationIdsAndCampaignAsync(
                    request.JobId,
                    request.ApplicationIds,
                    request.CampaignId
                );

                if (applications.Count != request.ApplicationIds.Count)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "One or more applications not found for this job and campaign."
                    };
                }

                // Load full application details with Resume data
                var applicationDetails = new List<ResumeApplication>();
                foreach (var appId in request.ApplicationIds)
                {
                    var appDetail = await _resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(appId);
                    if (appDetail == null || appDetail.Resume == null || string.IsNullOrEmpty(appDetail.Resume.Data))
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = $"Application {appId} does not have parsed resume data. Please ensure all resumes are processed before comparing."
                        };
                    }
                    applicationDetails.Add(appDetail);
                }

                // Generate queue job ID
                var queueJobId = Guid.NewGuid().ToString();

                // Prepare criteria data for queue
                var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                // Extract job details
                var skillsList = await jobRepo.GetSkillsByJobIdAsync(request.JobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(request.JobId);
                var languagesList = await jobRepo.GetLanguagesByJobIdAsync(request.JobId);

                // Prepare candidates data
                var candidatesData = applicationDetails.Select(app => new CandidateComparisonData
                {
                    applicationId = app.ApplicationId,
                    parsedData = !string.IsNullOrEmpty(app.Resume?.Data)
                        ? JsonSerializer.Deserialize<object>(app.Resume.Data)
                        : null,
                    matchSkills = app.MatchSkills,
                    missingSkills = app.MissingSkills,
                    totalScore = app.TotalScore
                }).ToList();

                await _uow.BeginTransactionAsync();
                int comparisonId;
                try
                {
                    // ✅ STEP 1: Atomic check and increment comparison counter (CRITICAL - prevents race condition)
                    var limitCheckInTransaction = await _comparisonLimitService.CheckComparisonLimitInTransactionAsync(companyId);
                    if (limitCheckInTransaction.Status != SRStatus.Success)
                    {
                        await _uow.RollbackTransactionAsync();
                        return limitCheckInTransaction;
                    }
                    // Counter has been incremented atomically - quota reserved

                    // ✅ STEP 2: Create Comparison record in DB
                    var comparison = new Comparison
                    {
                        JobId = request.JobId,
                        CampaignId = request.CampaignId,
                        CompanyId = companyId,
                        QueueJobId = queueJobId,
                        Status = ComparisonStatusEnum.Pending,
                        ResultJson = null // Will be filled by AI callback
                    };
                    await comparisonRepo.CreateAsync(comparison);
                    await _uow.SaveChangesAsync();

                    comparisonId = comparison.ComparisonId;

                    // Create ApplicationComparison links
                    foreach (var appId in request.ApplicationIds)
                    {
                        var appComparison = new ApplicationComparison
                        {
                            ApplicationId = appId,
                            ComparisonId = comparisonId
                        };
                        await applicationComparisonRepo.CreateAsync(appComparison);
                    }
                    await _uow.SaveChangesAsync();
                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                // Push job to Redis queue
                var jobData = new ComparisonQueueJobResponse
                {
                    comparisonId = comparisonId,
                    queueJobId = queueJobId,
                    companyId = companyId,
                    campaignId = request.CampaignId,
                    jobId = request.JobId,
                    jobTitle = job.Title,
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                    languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                    specialization = job.Specialization?.Name,
                    employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                    criteria = criteriaData,
                    level = job.Level?.Name,
                    candidates = candidatesData
                };

                // Push to Redis asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pushed = await _redisHelper.PushJobAsync("candidate_comparison_queue", jobData);
                        if (!pushed)
                        {
                            Console.WriteLine($"Warning: Failed to push comparison job to Redis queue for comparisonId: {comparisonId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error pushing comparison to Redis queue for comparisonId {comparisonId}: {ex.Message}");
                    }
                });

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Comparison initiated successfully.",
                    Data = new
                    {
                        ComparisonId = comparisonId,
                        QueueJobId = queueJobId,
                        ApplicationIds = request.ApplicationIds
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error comparing applications: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while comparing applications."
                };
            }
        }

        public async Task<ServiceResponse> ProcessComparisonAIResultAsync(ComparisonAIResultRequest request)
        {
            if (request == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Request is required."
                };
            }

            try
            {
                var comparisonRepo = _uow.GetRepository<IComparisonRepository>();

                // Find Comparison by comparisonId
                var comparison = await comparisonRepo.GetByIdAsync(request.ComparisonId);
                if (comparison == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Comparison not found."
                    };
                }

                // Handle AI error
                if (!string.IsNullOrWhiteSpace(request.Error))
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        // Store error in ResultJson
                        comparison.ResultJson = JsonSerializer.Serialize(new
                        {
                            status = "error",
                            error = request.Error,
                            reason = request.Reason
                        });
                        comparison.Status = ComparisonStatusEnum.Failed;
                        comparison.ErrorMessage = request.Reason ?? request.Error;
                        comparison.ProcessedAt = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(request.ComparisonName))
                        {
                            comparison.ComparisonName = request.ComparisonName;
                        }

                        await comparisonRepo.UpdateAsync(comparison);
                        await _uow.CommitTransactionAsync();

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Comparison error recorded.",
                            Data = new
                            {
                                comparisonId = comparison.ComparisonId,
                                status = "error",
                                error = request.Error
                            }
                        };
                    }
                    catch
                    {
                        await _uow.RollbackTransactionAsync();
                        throw;
                    }
                }

                // Validate required fields
                if (request.ResultJson == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "ResultJson is required."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Save comparison result
                    comparison.ResultJson = JsonUtils.SerializeRawJsonSafe(request.ResultJson);
                    comparison.Status = ComparisonStatusEnum.Completed;
                    comparison.ProcessedAt = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(request.ComparisonName))
                    {
                        comparison.ComparisonName = request.ComparisonName;
                    }
                    await comparisonRepo.UpdateAsync(comparison);
                    await _uow.CommitTransactionAsync();

                    // TODO: Send SignalR update to notify frontend

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Comparison result saved successfully.",
                        Data = new { comparisonId = comparison.ComparisonId }
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
                Console.WriteLine($"❌ Error processing comparison AI result: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while processing comparison result."
                };
            }
        }

        public async Task<ServiceResponse> GetComparisonResultAsync(int comparisonId)
        {
            try
            {
                // Get current user and company
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
                var comparisonRepo = _uow.GetRepository<IComparisonRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get comparison with details
                var comparison = await comparisonRepo.GetByIdAsync(comparisonId);

                if (comparison == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Comparison not found."
                    };
                }

                // Validate comparison belongs to user's company
                if (comparison.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this comparison."
                    };
                }

                // Parse result JSON if available
                object? resultData = null;
                if (!string.IsNullOrEmpty(comparison.ResultJson))
                {
                    try
                    {
                        resultData = JsonSerializer.Deserialize<object>(comparison.ResultJson);
                    }
                    catch
                    {
                        resultData = comparison.ResultJson; // Return as string if parsing fails
                    }
                }

                var response = new ComparisonDetailResponse
                {
                    ComparisonId = comparison.ComparisonId,
                    JobId = comparison.JobId,
                    CampaignId = comparison.CampaignId,
                    ComparisonName = comparison.ComparisonName,
                    Status = comparison.Status.ToString(),
                    ResultData = resultData,
                    ErrorMessage = comparison.ErrorMessage,
                    ProcessedAt = comparison.ProcessedAt,
                    CreatedAt = comparison.CreatedAt,
                    HasResult = !string.IsNullOrEmpty(comparison.ResultJson)
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Comparison retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting comparison result: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving comparison result."
                };
            }
        }

        public async Task<ServiceResponse> GetComparisonsByJobAndCampaignAsync(int jobId, int campaignId)
        {
            try
            {
                // Get current user and company
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
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var comparisonRepo = _uow.GetRepository<IComparisonRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job belongs to company
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null || job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view comparisons for this job."
                    };
                }

                // Get all comparisons for this job and campaign
                var comparisons = await comparisonRepo.GetByJobIdAndCampaignIdAsync(jobId, campaignId);

                var response = comparisons.Select(c => new ComparisonResponse
                {
                    ComparisonId = c.ComparisonId,
                    JobId = c.JobId,
                    CampaignId = c.CampaignId,
                    ComparisonName = c.ComparisonName,
                    Status = c.Status.ToString(),
                    ProcessedAt = c.ProcessedAt,
                    CreatedAt = c.CreatedAt,
                    HasResult = !string.IsNullOrEmpty(c.ResultJson)
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Comparisons retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting comparisons: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving comparisons."
                };
            }
        }
    }
}

