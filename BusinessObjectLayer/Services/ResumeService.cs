using BusinessObjectLayer.Common;
using BusinessObjectLayer.Hubs;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace BusinessObjectLayer.Services
{
    public class ResumeService : IResumeService
    {
        private readonly IUnitOfWork _uow;
        private readonly AICESDbContext _context;
        private readonly GoogleCloudStorageHelper _storageHelper;
        private readonly RedisHelper _redisHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IResumeLimitService _resumeLimitService;
        private readonly IHubContext<ResumeHub> _hubContext;
        private readonly IResumeApplicationRepository _resumeApplicationRepo;
        
        // Application-level lock per company to prevent race conditions
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _companyLocks = new();

        public ResumeService(
            IUnitOfWork uow,
            AICESDbContext context,
            GoogleCloudStorageHelper storageHelper,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor,
            IResumeLimitService resumeLimitService,
            IHubContext<ResumeHub> hubContext,
            IResumeApplicationRepository resumeApplicationRepo)
        {
            _uow = uow;
            _context = context;
            _storageHelper = storageHelper;
            _redisHelper = redisHelper;
            _httpContextAccessor = httpContextAccessor;
            _resumeLimitService = resumeLimitService;
            _hubContext = hubContext;
            _resumeApplicationRepo = resumeApplicationRepo;
        }

        public async Task<ServiceResponse> UploadResumeAsync(int campaignId, int jobId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "File is required."
                };
            }

            try
            {
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                var campaignRepo = _uow.GetRepository<ICampaignRepository>();

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

                // Validate campaign exists and belongs to user's company
                var campaign = await campaignRepo.GetByIdAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                if (campaign.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to access this campaign."
                    };
                }

                // Validate job exists and belongs to user's company
                var job = await jobRepo.GetJobByIdAsync(jobId);
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
                        Message = "This job does not exist in your company."
                    };
                }

                // Validate that job belongs to the campaign
                var jobCampaign = await _context.JobCampaigns
                    .Where(jc => jc.JobId == jobId && jc.CampaignId == campaignId)
                    .FirstOrDefaultAsync();

                if (jobCampaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "This job does not belong to the specified campaign."
                    };
                }

                // Check if company has an active subscription
                var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
                
                // Check if subscription is Active (not just Pending) - chỉ check nếu có subscription
                if (companySubscription != null && companySubscription.SubscriptionStatus != SubscriptionStatusEnum.Active)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Your company subscription is not active. Please wait for activation or contact support."
                    };
                }
                // Nếu không có subscription, company đang ở Free plan - cho phép upload

                // Check resume limit before uploading (early check for fast fail)
                var limitCheck = await _resumeLimitService.CheckResumeLimitAsync(companyId);
                if (limitCheck.Status != SRStatus.Success)
                {
                    return limitCheck;
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                string publicId = 
                    $"resumes/{companyId}/{jobId}/{userId}_{DateTime.UtcNow.Ticks}{extension}";

                // Upload file to Google Cloud Storage
                var uploadResult = await _storageHelper.UploadFileAsync(file, "resumes", publicId);

                if (uploadResult.Status != SRStatus.Success)
                {
                    return uploadResult;
                }

                // Extract objName from upload result
                string? fileUrl = null;
                if (uploadResult.Data != null)
                {
                    // Use reflection to extract URL (property name is "Url" with capital U)
                    var dataType = uploadResult.Data.GetType();
                    var urlProp = dataType.GetProperty("Url");
                    if (urlProp != null)
                    {
                        fileUrl = urlProp.GetValue(uploadResult.Data) as string;
                    }
                }

                if (string.IsNullOrEmpty(fileUrl))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to get URL from upload result."
                    };
                }

                // Compute file hash
                var fileHash = HashUtils.ComputeFileHash(file);

                // Generate queue job ID
                var queueJobId = Guid.NewGuid().ToString();

                // Get or create semaphore for this company to prevent concurrent uploads
                var semaphore = _companyLocks.GetOrAdd(companyId, _ => new SemaphoreSlim(1, 1));
                
                await semaphore.WaitAsync();
                try
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        // Check resume limit before creating record to fail fast
                        var limitCheckInTransaction = await _resumeLimitService.CheckResumeLimitInTransactionAsync(companyId);
                        if (limitCheckInTransaction.Status != SRStatus.Success)
                        {
                            await _uow.RollbackTransactionAsync();
                            return limitCheckInTransaction;
                        }

                        // Create Resume record (no longer has JobId)
                        var resume = new Resume
                        {
                            CompanyId = companyUser.CompanyId.Value,
                            QueueJobId = queueJobId,
                            FileUrl = fileUrl,
                            FileHash = fileHash,
                            Status = ResumeStatusEnum.Pending
                        };

                        await resumeRepo.CreateAsync(resume);
                        await _uow.SaveChangesAsync(); // Get ResumeId

                        // Create ResumeApplication to link Resume to Job and Campaign
                        var resumeApplication = new ResumeApplication
                        {
                            ResumeId = resume.ResumeId,
                            JobId = jobId,
                            CampaignId = campaignId,
                            Status = ApplicationStatusEnum.Pending
                        };
                        await _resumeApplicationRepo.CreateAsync(resumeApplication);
                        await _uow.SaveChangesAsync();

                        // Prepare criteria data for queue
                        var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                        {
                            criteriaId = c.CriteriaId,
                            name = c.Name,
                            weight = c.Weight
                        }).ToList() ?? new List<CriteriaQueueResponse>();

                        // Extract skills and employment types from repository
                        var skillsList = await jobRepo.GetSkillsByJobIdAsync(jobId);
                        var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(jobId);
                        var languagesList = await jobRepo.GetLanguagesByJobIdAsync(jobId);

                        // Push job to Redis queue 
                        var jobData = new ResumeQueueJobResponse
                        {
                            resumeId = resume.ResumeId,
                            queueJobId = queueJobId,
                            campaignId = campaignId,
                            jobId = jobId,
                            jobTitle = job.Title,
                            fileUrl = fileUrl,
                            requirements = job.Requirements,
                            skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                            languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                            specialization = job.Specialization?.Name,
                            employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                            criteria = criteriaData,
                            level = job.Level?.Name,
                            mode = "parse"
                        };

                        var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                        if (!pushed)
                        {
                            // Log warning but don't fail the request
                            Console.WriteLine($"Warning: Failed to push job to Redis queue for resumeId: {resume.ResumeId}");
                        }

                        // Store job data with queueJobId as key for tracking and retrieval (expires in 24 hours)
                        // await _redisHelper.SetJobDataAsync($"resume:job:{queueJobId}", jobData, TimeSpan.FromHours(24));
                        // // Also store with resumeId for easy lookup
                        // await _redisHelper.SetJobDataAsync($"resume:data:{parsedResume.ResumeId}", jobData, TimeSpan.FromHours(24));

                        await _uow.CommitTransactionAsync();

                        // Reload resume for SignalR to get fresh data
                        var uploadedResume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resume.ResumeId);
                        
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(jobId, resume.ResumeId, "uploaded", null, uploadedResume);
                        });

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Resume uploaded successfully.",
                            Data = new ResumeUploadResponse
                            {
                                ResumeId = resume.ResumeId,
                                QueueJobId = queueJobId,
                                Status = ResumeStatusEnum.Pending
                            }
                        };
                    }
                    catch
                    {
                        await _uow.RollbackTransactionAsync();
                        throw;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error uploading resume: {ex.Message}");
                Console.WriteLine($"📋 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                
                // Log inner exception if exists
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"💥 Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"🔍 Inner Stack trace: {ex.InnerException.StackTrace}");
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while uploading the resume: {ex.Message}",
                    Data = new ResumeUploadResponse
                    {
                        Status = ResumeStatusEnum.Failed
                    }
                };;
            }
        }

        public async Task<ServiceResponse> ResendResumeAsync(int jobId, int resumeId)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to resend this resume."
                    };
                }

                // Get resume with parsed data
                var resume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // Check if resume has been parsed (must have Data)
                if (string.IsNullOrEmpty(resume.Data))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Resume has not been parsed yet. Cannot rescore."
                    };
                }

                // Check if resume status is Completed
                if (resume.Status != ResumeStatusEnum.Completed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Cannot rescore resume with status '{resume.Status}'. Only 'Completed' resumes can be rescored."
                    };
                }

                // Get ResumeApplication to retrieve campaign context
                var resumeApplication = await _resumeApplicationRepo.GetByResumeIdAndJobIdAsync(resumeId, jobId);
                if (resumeApplication == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "ResumeApplication not found for this resume."
                    };
                }

                // Generate new queue job ID
                var newQueueJobId = Guid.NewGuid().ToString();

                // Prepare criteria data for queue
                var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                // Parse the existing resume data JSON
                object? parsedData = null;
                try
                {
                    parsedData = JsonSerializer.Deserialize<object>(resume.Data);
                }
                catch
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to parse existing resume data."
                    };
                }

                // Extract skills and employment types from repository
                var skillsList = await jobRepo.GetSkillsByJobIdAsync(jobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(jobId);
                var languagesList = await jobRepo.GetLanguagesByJobIdAsync(jobId);

                // Push job to Redis queue with mode = "rescore" and parsedData
                var jobData = new ResumeQueueJobResponse
                {
                    resumeId = resume.ResumeId,
                    queueJobId = newQueueJobId,
                    campaignId = resumeApplication.CampaignId ?? 0,
                    jobId = jobId,
                    jobTitle = job.Title,
                    fileUrl = resume.FileUrl ?? string.Empty,
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                    languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                    specialization = job.Specialization?.Name,
                    employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                    criteria = criteriaData,
                    mode = "rescore",
                    parsedData = parsedData
                };

                var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                if (!pushed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to push job to Redis queue."
                    };
                }

                // Store rescore job data with queueJobId as key (expires in 24 hours)
                // await _redisHelper.SetJobDataAsync($"resume:job:{newQueueJobId}", jobData, TimeSpan.FromHours(24));
                // // Update the resume data key with new job info
                // await _redisHelper.SetJobDataAsync($"resume:data:{resume.ResumeId}", jobData, TimeSpan.FromHours(24));

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update resume: new queueJobId and status = Pending
                    resume.QueueJobId = newQueueJobId;
                    resume.Status = ResumeStatusEnum.Pending;
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var rescoreResume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resume.ResumeId);
                    
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobId, resume.ResumeId, "rescore_initiated", 
                            new { newStatus = "Pending" }, rescoreResume);
                    });
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume rescore initiated successfully.",
                    Data = new ResumeUploadResponse
                    {
                        ResumeId = resume.ResumeId,
                        QueueJobId = newQueueJobId,
                        Status = ResumeStatusEnum.Pending
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error resending resume for rescore: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while resending the resume for rescore."
                };
            }
        }

        public async Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                var candidateRepo = _uow.GetRepository<ICandidateRepository>();
                var scoreDetailRepo = _uow.GetRepository<IScoreDetailRepository>();
                
                // 1. Find resume
                var resume = await resumeRepo.GetByQueueJobIdAsync(request.QueueJobId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found for the given queue job ID."
                    };
                }

                    // Get ResumeApplication for this resume (needed for JobId and to update scores)
                    var resumeApplication = await _resumeApplicationRepo.GetByResumeIdAsync(resume.ResumeId);

                    if (resumeApplication == null)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.NotFound,
                            Message = "ResumeApplication not found for this resume."
                        };
                    }

                    // 1.5 Handle AI-side validation errors
                if (!string.IsNullOrWhiteSpace(request.Error))
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        // Determine status based on error type
                        ResumeStatusEnum errorStatus;
                        string errorMessage;
                        
                        switch (request.Error.ToLower())
                        {
                            case "invalid_resume_data":
                                errorStatus = ResumeStatusEnum.InvalidResumeData;
                                errorMessage = request.Reason ?? "The uploaded file is not a valid resume.";
                                break;
                            
                            case "invalid_job_data":
                                errorStatus = ResumeStatusEnum.InvalidJobData;
                                errorMessage = request.Reason ?? "The job data is invalid.";
                                break;
                            
                            case "job_title_not_matched":
                                errorStatus = ResumeStatusEnum.JobTitleNotMatched;
                                errorMessage = request.Reason ?? "The candidate's experience does not match the job title requirements.";
                                break;
                            
                            default:
                                errorStatus = ResumeStatusEnum.Failed;
                                errorMessage = request.Reason ?? $"Error: {request.Error}";
                                break;
                        }

                        resume.Status = errorStatus;
                        resume.ErrorMessage = errorMessage;
                        resume.Data = null; // keep Data null on error
                        
                        // Update ResumeApplication status to Failed for resume errors
                        resumeApplication.Status = ApplicationStatusEnum.Failed;
                        await _resumeApplicationRepo.UpdateAsync(resumeApplication);

                        await resumeRepo.UpdateAsync(resume);
                        await _uow.CommitTransactionAsync();

                        // Reload resume for SignalR
                        var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                        
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(resumeApplication.JobId, resume.ResumeId, "status_changed", 
                                new { newStatus = errorStatus.ToString() }, resumeForSignalR);
                        });

                        // Return appropriate message based on error type
                        string responseMessage = request.Error.ToLower() switch
                        {
                            "invalid_resume_data" => "The uploaded file is not a valid resume.",
                            "invalid_job_data" => "The job data is invalid.",
                            "job_title_not_matched" => "The candidate's experience does not match the job title requirements.",
                            _ => "An error occurred while processing the resume."
                        };

                        // Return Success to AI callback to avoid retries; store state in DB
                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = responseMessage,
                            Data = new
                            {
                                resumeId = resume.ResumeId,
                                status = errorStatus.ToString(),
                                error = request.Error,
                                reason = request.Reason
                            }
                        };
                    }
                    catch
                    {
                        await _uow.RollbackTransactionAsync();
                        throw;
                    }
                }

                // 2. Validate resumeId
                if (resume.ResumeId != request.ResumeId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Resume ID mismatch."
                    };
                }

                // 2.5 Validate required scoring fields when there's no AI error
                if (request.TotalResumeScore == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Total resume score is required."
                    };
                }

                if (request.ScoreDetails == null || request.ScoreDetails.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "ScoreDetails is required."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // 3. Update parsed resume (JSON + Completed)
                    resume.Status = ResumeStatusEnum.Completed;

                    if (request.RawJson != null)
                    {
                        resume.Data = JsonUtils.SerializeRawJsonSafe(request.RawJson);
                    }

                    await resumeRepo.UpdateAsync(resume);
                    await _uow.SaveChangesAsync();

                    // 4. Create/Update Candidate first
                    var existingCandidate = await candidateRepo.GetByResumeIdAsync(resume.ResumeId);

                    var fullName = request.CandidateInfo?.FullName ?? "Unknown";
                    var email = request.CandidateInfo?.Email ?? "unknown@example.com";
                    var phone = request.CandidateInfo?.PhoneNumber;
                    var matchSkills = request.CandidateInfo?.MatchSkills;
                    var missingSkills = request.CandidateInfo?.MissingSkills;

                    Candidate candidate;
                    if (existingCandidate == null)
                    {
                        candidate = new Candidate
                        {
                            JobId = resumeApplication.JobId,
                            FullName = fullName,
                            Email = email,
                            PhoneNumber = phone,
                            MatchSkills = matchSkills,
                            MissingSkills = missingSkills
                        };

                        await candidateRepo.CreateAsync(candidate);
                        await _uow.SaveChangesAsync(); // Get CandidateId
                        
                        // Link resume to candidate
                        resume.CandidateId = candidate.CandidateId;
                    }
                    else
                    {
                        existingCandidate.FullName = fullName;
                        existingCandidate.Email = email;
                        existingCandidate.PhoneNumber = phone;
                        existingCandidate.MatchSkills = matchSkills;
                        existingCandidate.MissingSkills = missingSkills;

                        await candidateRepo.UpdateAsync(existingCandidate);
                        candidate = existingCandidate;
                        
                        // Link resume to candidate
                        resume.CandidateId = candidate.CandidateId;
                    }

                    // 5. Save AI Score to ResumeApplication (not Resume)
                    string? aiExplanationString = request.AIExplanation != null
                        ? JsonUtils.SerializeRawJsonSafe(request.AIExplanation)
                        : null;

                    // Update ResumeApplication with scores
                    resumeApplication.TotalScore = request.TotalResumeScore.Value;
                    resumeApplication.Status = ApplicationStatusEnum.Reviewed;
                    resumeApplication.AIExplanation = aiExplanationString;
                    resumeApplication.RequiredSkills = request.RequiredSkills;
                    await _resumeApplicationRepo.UpdateAsync(resumeApplication);

                    // Update Resume and mark as latest
                    resume.IsLatest = true; // Mark as latest resume
                    
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.SaveChangesAsync();

                    // 6. Save ScoreDetails (now linked to ResumeApplication, not Resume)
                    var scoreDetails = request.ScoreDetails.Select(detail => new ScoreDetail
                    {
                        CriteriaId = detail.CriteriaId,
                        ApplicationId = resumeApplication.ApplicationId,
                        Matched = detail.Matched,
                        Score = detail.Score,
                        AINote = detail.AINote
                    }).ToList();

                    await scoreDetailRepo.CreateRangeAsync(scoreDetails);
                    await _uow.SaveChangesAsync();
                    await _uow.CommitTransactionAsync();

                    // Reload resume with all includes to get fresh data for SignalR
                    var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                    
                    // Send real-time SignalR update (fire and forget, but with proper data)
                    _ = Task.Run(async () => 
                    {
                        // Small delay to ensure all changes are persisted
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(resumeApplication.JobId, resume.ResumeId, "status_changed", 
                            new { newStatus = "Completed" }, resumeForSignalR);
                    });

                    // 7. Response
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "AI result saved successfully.",
                        Data = new { resumeId = resume.ResumeId }
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
                Console.WriteLine($"Error processing AI result: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                try
                {
                    var resumeRepo = _uow.GetRepository<IResumeRepository>();
                    var parsedResume = await resumeRepo.GetByQueueJobIdAsync(request.QueueJobId);
                    if (parsedResume != null)
                    {
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            parsedResume.Status = ResumeStatusEnum.Failed;
                            await resumeRepo.UpdateAsync(parsedResume);
                            await _uow.CommitTransactionAsync();

                        // Get ResumeApplication to find JobId
                            var failedResumeApplication = await _resumeApplicationRepo.GetByResumeIdAsync(parsedResume.ResumeId);
                            
                            if (failedResumeApplication != null)
                            {
                                // Reload resume for SignalR
                                var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(failedResumeApplication.JobId, parsedResume.ResumeId);
                                
                                // Send real-time SignalR update
                                _ = Task.Run(async () => 
                                {
                                    await Task.Delay(200);
                                    await SendResumeUpdateAsync(failedResumeApplication.JobId, parsedResume.ResumeId, "status_changed", 
                                        new { newStatus = "Failed" }, resumeForSignalR);
                                });
                            }
                        }
                        catch
                        {
                            await _uow.RollbackTransactionAsync();
                        }
                    }
                }
                catch { /* ignore logging errors */ }

                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while processing the AI result."
                };
            }
        }

        public async Task<ServiceResponse> GetJobResumesAsync(int jobId, int campaignId)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view resumes for this job."
                    };
                }

                // Get ResumeApplications filtered by job and campaign with Resume & Candidate included
                var resumeApplications = await _resumeApplicationRepo.GetByJobIdAndCampaignWithResumeAsync(jobId, campaignId);

                var resumeList = resumeApplications
                    .Select(application => new JobResumeListResponse
                    {
                        ResumeId = application.ResumeId,
                        ApplicationId = application.ApplicationId,
                        Status = application.Resume?.Status ?? ResumeStatusEnum.Pending,
                        ApplicationStatus = application.Status,
                        FullName = application.Resume?.Candidate?.FullName ?? "Unknown",
                        TotalScore = application.TotalScore,
                        AdjustedScore = application.AdjustedScore
                    })
                    .ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job resumes retrieved successfully.",
                    Data = resumeList
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting job resumes: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving job resumes."
                };
            }
        }

        public async Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int applicationId, int campaignId)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to view this resume."
                    };
                }

                // Get ResumeApplication with details
                var resumeApplication = await _resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(applicationId);

                if (resumeApplication == null || resumeApplication.JobId != jobId || resumeApplication.CampaignId != campaignId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found for this job and campaign."
                    };
                }

                var resume = resumeApplication.Resume;
                var candidate = resume?.Candidate;
                
                // Get score details from ResumeApplication
                var scoreDetailsResponse = resumeApplication?.ScoreDetails?
                    .Select(detail => new ResumeScoreDetailResponse
                    {
                        CriteriaId = detail.CriteriaId,
                        CriteriaName = detail.Criteria?.Name ?? "",
                        Matched = detail.Matched,
                        Score = detail.Score,
                        AINote = detail.AINote
                    }).ToList() ?? new List<ResumeScoreDetailResponse>();

                var response = new JobResumeDetailResponse
                {
                    ResumeId = resume?.ResumeId,
                    ApplicationId = resumeApplication?.ApplicationId,
                    QueueJobId = resume?.QueueJobId ?? string.Empty,
                    FileUrl = resume?.FileUrl ?? string.Empty,
                    Status = resume?.Status ?? ResumeStatusEnum.Pending,
                    ApplicationStatus = resumeApplication?.Status ?? ApplicationStatusEnum.Pending,
                    CampaignId = resumeApplication?.CampaignId,
                    CreatedAt = resume?.CreatedAt,
                    CandidateId = candidate?.CandidateId ?? 0,
                    FullName = candidate?.FullName ?? "Unknown",
                    Email = candidate?.Email ?? "N/A",
                    PhoneNumber = candidate?.PhoneNumber,
                    MatchSkills = candidate?.MatchSkills,
                    MissingSkills = candidate?.MissingSkills,
                    TotalScore = resumeApplication?.TotalScore,
                    AdjustedScore = resumeApplication?.AdjustedScore,
                    AIExplanation = resumeApplication?.AIExplanation,
                    ErrorMessage = resume?.ErrorMessage,
                    ScoreDetails = scoreDetailsResponse
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume detail retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting job resume detail: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving resume detail."
                };
            }
        }

        public async Task<ServiceResponse> RetryFailedResumeAsync(int resumeId)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get resume
                var resume = await resumeRepo.GetForUpdateAsync(resumeId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // Validate company ownership
                if (resume.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to retry this resume."
                    };
                }

                // Check if resume status is Failed
                if (resume.Status != ResumeStatusEnum.Failed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Cannot retry resume with status '{resume.Status}'. Only 'Failed' resumes can be retried."
                    };
                }

                // Check if resume is active
                if (!resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Cannot retry an inactive resume."
                    };
                }

                // Get ResumeApplication to find JobId
                var resumeApplication = await _resumeApplicationRepo.GetByResumeIdAsync(resumeId);

                if (resumeApplication == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "ResumeApplication not found for this resume."
                    };
                }

                // Get job with requirements and criteria
                var job = await jobRepo.GetJobByIdAsync(resumeApplication.JobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                // Generate new queue job ID
                var newQueueJobId = Guid.NewGuid().ToString();

                // Prepare criteria data for queue
                var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                // Extract skills and employment types from repository
                var skillsList = await jobRepo.GetSkillsByJobIdAsync(resumeApplication.JobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(resumeApplication.JobId);
                var languagesList = await jobRepo.GetLanguagesByJobIdAsync(resumeApplication.JobId);

                // Push job to Redis queue with requirements and criteria
                var jobData = new ResumeQueueJobResponse
                {
                    resumeId = resume.ResumeId,
                    queueJobId = newQueueJobId,
                    campaignId = resumeApplication.CampaignId ?? 0,
                    jobId = resumeApplication.JobId,
                    jobTitle = job.Title,
                    fileUrl = resume.FileUrl ?? string.Empty,
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                    languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                    specialization = job.Specialization?.Name,
                    employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                    criteria = criteriaData,
                    mode = "parse"
                };

                var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                if (!pushed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to push job to Redis queue."
                    };
                }

                // Store retry job data with queueJobId as key (expires in 24 hours)
                // await _redisHelper.SetJobDataAsync($"resume:job:{newQueueJobId}", jobData, TimeSpan.FromHours(24));
                // // Update the resume data key with new job info
                // await _redisHelper.SetJobDataAsync($"resume:data:{resume.ResumeId}", jobData, TimeSpan.FromHours(24));

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update resume: new queueJobId and status = Pending
                    resume.QueueJobId = newQueueJobId;
                    resume.Status = ResumeStatusEnum.Pending;
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var retryResume = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                    
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(resumeApplication.JobId, resume.ResumeId, "retried", 
                            new { newStatus = "Pending" }, retryResume);
                    });
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume retry initiated successfully.",
                    Data = new ResumeUploadResponse
                    {
                        ResumeId = resume.ResumeId,
                        QueueJobId = newQueueJobId,
                        Status = ResumeStatusEnum.Pending
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error retrying failed resume: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrying the resume."
                };
            }
        }

        public async Task<ServiceResponse> SoftDeleteResumeAsync(int resumeId)
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
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get resume
                var resume = await resumeRepo.GetForUpdateAsync(resumeId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // Validate company ownership
                if (resume.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to delete this resume."
                    };
                }

                // Check if already deleted
                if (!resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Resume is already deleted."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Soft delete: set IsActive = false
                    resume.IsActive = false;
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Get ResumeApplication to find JobId
                    var deletedResumeApplication = await _resumeApplicationRepo.GetByResumeIdAsync(resumeId);

                    if (deletedResumeApplication != null)
                    {
                        // Note: For deleted resumes, we still send update but resume will be null in query
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(deletedResumeApplication.JobId, resume.ResumeId, "deleted", null, resume);
                        });
                    }
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume deleted successfully.",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting resume: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting the resume."
                };
            }
        }

        /// <summary>
        /// Send real-time SignalR update to clients watching a job's resumes
        /// </summary>
        private async Task SendResumeUpdateAsync(int jobId, int resumeId, string eventType, object? data = null, Resume? resume = null)
        {
            try
            {
                // If resume is not provided, query it (with small delay to ensure transaction committed)
                if (resume == null)
                {
                    await Task.Delay(100); // Small delay to ensure transaction is committed
                    var resumeRepo = _uow.GetRepository<IResumeRepository>();
                    resume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                }
                
                if (resume == null)
                {
                    Console.WriteLine($"⚠️ SignalR: Resume {resumeId} not found, cannot send update");
                    return;
                }

                // Get ResumeApplication for scores
                var resumeApplication = await _resumeApplicationRepo.GetByResumeIdAndJobIdAsync(resumeId, jobId);

                var updateData = new
                {
                    eventType, // "uploaded", "status_changed", "deleted", "retried"
                    jobId,
                    resumeId,
                    status = resume.Status.ToString(),
                    fullName = resume.Candidate?.FullName ?? "Unknown",
                    totalResumeScore = resumeApplication?.AdjustedScore ?? resumeApplication?.TotalScore,
                    timestamp = DateTime.UtcNow,
                    data
                };

                // Send to job group (all users watching this job)
                await _hubContext.Clients.Group($"job-{jobId}")
                    .SendAsync("ResumeUpdated", updateData);

                Console.WriteLine($"📡 SignalR: Sent {eventType} update for resume {resumeId} in job {jobId} - Status: {resume.Status}, Score: {updateData.totalResumeScore}");
            }
            catch (Exception ex)
            {
                // Don't fail the main operation if SignalR fails
                Console.WriteLine($"⚠️ Error sending SignalR update: {ex.Message}");
                Console.WriteLine($"⚠️ Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Send real-time SignalR update for resume list changes (when list needs refresh)
        /// </summary>
        private async Task SendResumeListUpdateAsync(int jobId, string eventType)
        {
            try
            {
                var updateData = new
                {
                    eventType, // "list_changed"
                    jobId,
                    timestamp = DateTime.UtcNow
                };

                // Send to job group (all users watching this job)
                await _hubContext.Clients.Group($"job-{jobId}")
                    .SendAsync("ResumeListUpdated", updateData);

                Console.WriteLine($"📡 SignalR: Sent list update for job {jobId}");
            }
            catch (Exception ex)
            {
                // Don't fail the main operation if SignalR fails
                Console.WriteLine($"⚠️ Error sending SignalR list update: {ex.Message}");
            }
        }
    }
}

