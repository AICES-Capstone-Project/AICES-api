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
            GoogleCloudStorageHelper storageHelper,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor,
            IResumeLimitService resumeLimitService,
            IHubContext<ResumeHub> hubContext,
            IResumeApplicationRepository resumeApplicationRepo)
        {
            _uow = uow;
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
                var jobCampaign = await campaignRepo.GetJobCampaignByJobIdAndCampaignIdAsync(jobId, campaignId);

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

                // ✅ OPTIONAL: Early check for fast fail (không consume quota nếu fail sớm)
                var earlyLimitCheck = await _resumeLimitService.CheckResumeLimitAsync(companyId);
                if (earlyLimitCheck.Status != SRStatus.Success)
                {
                    return earlyLimitCheck;
                }

                // Compute file hash early to detect reuse/duplicate
                var fileHash = HashUtils.ComputeFileHash(file);

                // Check for duplicate resume: same fileHash, jobId, campaignId, and status = Completed
                var isDuplicate = await _resumeApplicationRepo.IsDuplicateResumeAsync(jobId, campaignId, fileHash);
                if (isDuplicate)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "This resume has already been uploaded and processed for this job and campaign."
                    };
                }

                // Check if this file already exists in the company (reuse resume)
                var existingResume = await resumeRepo.GetByFileHashAndCompanyIdAsync(companyId, fileHash);

                // Flow logic:
                // 1. If resume exists + same job + already scored → Clone result (no quota consumed)
                // 2. If resume exists + different job → Reuse resume (quota consumed for new application)
                // 3. If resume not exists → Create new (quota consumed)
                
                ResumeApplication? existingApplication = null;
                bool shouldCloneResult = false;
                bool reuseExistingResume = false;

                if (existingResume != null)
                {
                    // Check if there's already an application for this resume + job combination
                    existingApplication = await _resumeApplicationRepo.GetByResumeIdAndJobIdWithDetailsAsync(existingResume.ResumeId, jobId);
                    
                    if (existingApplication != null && existingApplication.Status == ApplicationStatusEnum.Reviewed)
                    {
                        // Same resume + same job + already scored → Clone result (NO quota consumed)
                        shouldCloneResult = true;
                    }
                    else if (existingResume.Status == ResumeStatusEnum.Completed && existingResume.Data != null)
                    {
                        // Resume exists and completed → Can reuse for different job (quota consumed)
                        reuseExistingResume = true;
                    }
                    else
                    {
                        // Resume exists but not completed or no data → reject
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "This file has already been uploaded for this company but could not be fully processed. Please upload a different resume file."
                        };
                    }
                }

                // Prepare data for queue (outside transaction)
                var queueJobId = Guid.NewGuid().ToString();
                var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                var skillsList = await jobRepo.GetSkillsByJobIdAsync(jobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(jobId);
                var languagesList = await jobRepo.GetLanguagesByJobIdAsync(jobId);

                // ✅ NEW FLOW: Semaphore → Transaction → Check&Increment → Create Resume → Commit → Upload File
                var semaphore = _companyLocks.GetOrAdd(companyId, _ => new SemaphoreSlim(1, 1));
                
                int resumeId;
                int applicationId;
                string? resumeFileUrl = null;
                
                await semaphore.WaitAsync();
                try
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        // ✅ STEP 1: Atomic check and increment counter (CRITICAL - prevents race condition)
                        // Skip if cloning (cloning doesn't consume quota)
                        if (!shouldCloneResult)
                        {
                            var limitCheckInTransaction = await _resumeLimitService.CheckResumeLimitInTransactionAsync(companyId);
                            if (limitCheckInTransaction.Status != SRStatus.Success)
                            {
                                await _uow.RollbackTransactionAsync();
                                return limitCheckInTransaction;
                            }
                            // Counter has been incremented atomically - quota reserved
                        }

                        // ✅ STEP 2: Create Resume and Application in DB
                        if (shouldCloneResult && existingApplication != null && existingResume != null)
                        {
                            // Clone flow: Copy existing result
                            var clonedApplication = new ResumeApplication
                            {
                                ResumeId = existingResume.ResumeId,
                                JobId = jobId,
                                CampaignId = campaignId,
                                QueueJobId = queueJobId,
                                Status = ApplicationStatusEnum.Reviewed,
                                CandidateId = existingApplication.CandidateId,
                                TotalScore = existingApplication.TotalScore,
                                AdjustedScore = existingApplication.AdjustedScore,
                                AIExplanation = existingApplication.AIExplanation,
                                MatchSkills = existingApplication.MatchSkills,
                                MissingSkills = existingApplication.MissingSkills,
                                RequiredSkills = existingApplication.RequiredSkills,
                                ProcessingMode = ProcessingModeEnum.Clone,
                                ClonedFromApplicationId = existingApplication.ApplicationId,
                                ProcessedAt = DateTime.UtcNow,
                                ProcessingTimeMs = 0
                            };

                            await _resumeApplicationRepo.CreateAsync(clonedApplication);
                            await _uow.SaveChangesAsync();
                            
                            // Update reuse statistics
                            existingResume.ReuseCount++;
                            existingResume.LastReusedAt = DateTime.UtcNow;
                            await resumeRepo.UpdateAsync(existingResume);
                            await _uow.SaveChangesAsync();

                            // Clone ScoreDetails
                            var scoreDetailRepo = _uow.GetRepository<IScoreDetailRepository>();
                            var clonedScoreDetails = existingApplication.ScoreDetails?.Select(sd => new ScoreDetail
                            {
                                ApplicationId = clonedApplication.ApplicationId,
                                CriteriaId = sd.CriteriaId,
                                Matched = sd.Matched,
                                Score = sd.Score,
                                AINote = sd.AINote
                            }).ToList() ?? new List<ScoreDetail>();

                            if (clonedScoreDetails.Any())
                            {
                                await scoreDetailRepo.CreateRangeAsync(clonedScoreDetails);
                                await _uow.SaveChangesAsync();
                            }

                            resumeId = existingResume.ResumeId;
                            applicationId = clonedApplication.ApplicationId;
                            resumeFileUrl = existingResume.FileUrl;
                        }
                        else if (reuseExistingResume && existingResume != null)
                        {
                            // Reuse flow: Use existing resume for new application
                            existingResume.ReuseCount++;
                            existingResume.LastReusedAt = DateTime.UtcNow;
                            await resumeRepo.UpdateAsync(existingResume);
                            await _uow.SaveChangesAsync();

                            var resumeApplication = new ResumeApplication
                            {
                                ResumeId = existingResume.ResumeId,
                                JobId = jobId,
                                CampaignId = campaignId,
                                QueueJobId = queueJobId,
                                Status = ApplicationStatusEnum.Pending,
                                ProcessingMode = ProcessingModeEnum.Score // Rescore with existing data
                            };
                            await _resumeApplicationRepo.CreateAsync(resumeApplication);
                            await _uow.SaveChangesAsync();

                            resumeId = existingResume.ResumeId;
                            applicationId = resumeApplication.ApplicationId;
                            resumeFileUrl = existingResume.FileUrl;
                        }
                        else
                        {
                            // New flow: Create new resume (Pending status, FileUrl = null)
                            var resume = new Resume
                            {
                                CompanyId = companyUser.CompanyId.Value,
                                FileUrl = null, // ✅ Will be set after upload
                                OriginalFileName = file.FileName,
                                FileHash = fileHash,
                                Status = ResumeStatusEnum.Pending,
                                ReuseCount = 0
                            };

                            await resumeRepo.CreateAsync(resume);
                            await _uow.SaveChangesAsync();

                            var resumeApplication = new ResumeApplication
                            {
                                ResumeId = resume.ResumeId,
                                JobId = jobId,
                                CampaignId = campaignId,
                                QueueJobId = queueJobId,
                                Status = ApplicationStatusEnum.Pending,
                                ProcessingMode = ProcessingModeEnum.Parse
                            };
                            await _resumeApplicationRepo.CreateAsync(resumeApplication);
                            await _uow.SaveChangesAsync();

                            resumeId = resume.ResumeId;
                            applicationId = resumeApplication.ApplicationId;
                            // resumeFileUrl = null (will upload after commit)
                        }

                        // ✅ STEP 3: Commit transaction
                        await _uow.CommitTransactionAsync();
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

                // ✅ STEP 4: Upload file OUTSIDE transaction (for new resumes only)
                if (!shouldCloneResult && !reuseExistingResume)
                {
                    try
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        string publicId = $"resumes/{companyId}/{jobId}/{userId}_{DateTime.UtcNow.Ticks}{extension}";

                        var uploadResult = await _storageHelper.UploadFileAsync(file, "resumes", publicId);
                        if (uploadResult.Status != SRStatus.Success)
                        {
                            // Upload failed - mark resume as Failed
                            var resumeToUpdate = await resumeRepo.GetForUpdateAsync(resumeId);
                            if (resumeToUpdate != null)
                            {
                                resumeToUpdate.Status = ResumeStatusEnum.Failed;
                                await resumeRepo.UpdateAsync(resumeToUpdate);
                                await _uow.SaveChangesAsync();
                            }

                            return new ServiceResponse
                            {
                                Status = SRStatus.Error,
                                Message = $"Resume created but file upload failed: {uploadResult.Message}. Please try uploading again.",
                                Data = new ResumeUploadResponse
                                {
                                    ResumeId = resumeId,
                                    QueueJobId = queueJobId,
                                    Status = ResumeStatusEnum.Failed
                                }
                            };
                        }

                        // Extract file URL
                        string? uploadedFileUrl = null;
                        if (uploadResult.Data != null)
                        {
                            var dataType = uploadResult.Data.GetType();
                            var urlProp = dataType.GetProperty("Url");
                            if (urlProp != null)
                            {
                                uploadedFileUrl = urlProp.GetValue(uploadResult.Data) as string;
                            }
                        }

                        if (string.IsNullOrEmpty(uploadedFileUrl))
                        {
                            // Failed to get URL - mark as failed
                            var resumeToUpdate = await resumeRepo.GetForUpdateAsync(resumeId);
                            if (resumeToUpdate != null)
                            {
                                resumeToUpdate.Status = ResumeStatusEnum.Failed;
                                await resumeRepo.UpdateAsync(resumeToUpdate);
                                await _uow.SaveChangesAsync();
                            }

                            return new ServiceResponse
                            {
                                Status = SRStatus.Error,
                                Message = "Failed to get file URL from upload result.",
                                Data = new ResumeUploadResponse
                                {
                                    ResumeId = resumeId,
                                    QueueJobId = queueJobId,
                                    Status = ResumeStatusEnum.Failed
                                }
                            };
                        }

                        // ✅ STEP 5: Update Resume.FileUrl
                        var resumeToUpdateUrl = await resumeRepo.GetForUpdateAsync(resumeId);
                        if (resumeToUpdateUrl != null)
                        {
                            resumeToUpdateUrl.FileUrl = uploadedFileUrl;
                            await resumeRepo.UpdateAsync(resumeToUpdateUrl);
                            await _uow.SaveChangesAsync();
                        }

                        resumeFileUrl = uploadedFileUrl;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error uploading file: {ex.Message}");
                        
                        // Mark resume as failed
                        try
                        {
                            var resumeToUpdate = await resumeRepo.GetForUpdateAsync(resumeId);
                            if (resumeToUpdate != null)
                            {
                                resumeToUpdate.Status = ResumeStatusEnum.Failed;
                                await resumeRepo.UpdateAsync(resumeToUpdate);
                                await _uow.SaveChangesAsync();
                            }
                        }
                        catch { /* Ignore error updating status */ }

                        return new ServiceResponse
                        {
                            Status = SRStatus.Error,
                            Message = $"Resume created but file upload failed: {ex.Message}",
                            Data = new ResumeUploadResponse
                            {
                                ResumeId = resumeId,
                                QueueJobId = queueJobId,
                                Status = ResumeStatusEnum.Failed
                            }
                        };
                    }
                }

                // ✅ STEP 6: Push job to Redis queue (skip if cloning)
                if (!shouldCloneResult)
                {
                    var jobData = new ResumeQueueJobResponse
                    {
                        companyId = companyId,
                        resumeId = resumeId,
                        applicationId = applicationId,
                        queueJobId = queueJobId,
                        campaignId = campaignId,
                        jobId = jobId,
                        jobTitle = job.Title,
                        fileUrl = reuseExistingResume ? string.Empty : (resumeFileUrl ?? string.Empty),
                        requirements = job.Requirements,
                        skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                        languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                        specialization = job.Specialization?.Name,
                        employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                        criteria = criteriaData,
                        level = job.Level?.Name,
                        mode = (reuseExistingResume ? ProcessingModeEnum.Score : ProcessingModeEnum.Parse)
                            .ToString().ToLowerInvariant(),
                        parsedData = reuseExistingResume ? existingResume!.Data : null
                    };

                    // Push to Redis asynchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                            if (!pushed)
                            {
                                Console.WriteLine($"⚠️ Warning: Failed to push job to Redis queue for resumeId: {resumeId}");
                            }
                            else
                            {
                                Console.WriteLine($"✅ Pushed resume {resumeId} to Redis queue (mode: {jobData.mode})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error pushing to Redis queue for resumeId {resumeId}: {ex.Message}");
                        }
                    });
                }

                // Reload resume for SignalR (outside semaphore)
                var uploadedResume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                
                // Load application for SignalR (especially important for cloned results to show score)
                var uploadedApplication = await _resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(applicationId);
                
                // Capture data for SignalR BEFORE background task
                var jobIdForSignalR = jobId;
                var resumeIdForSignalR = resumeId;
                var eventTypeForSignalR = shouldCloneResult ? "cloned" : "uploaded";
                
                // Send real-time SignalR update
                _ = Task.Run(async () => 
                {
                    await Task.Delay(200);
                    await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, eventTypeForSignalR, null, uploadedResume, uploadedApplication);
                });

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = shouldCloneResult 
                        ? "Resume uploaded successfully. Using existing analysis result."
                        : "Resume uploaded successfully.",
                    Data = new ResumeUploadResponse
                    {
                        ResumeId = resumeId,
                        QueueJobId = queueJobId,
                        Status = shouldCloneResult ? ResumeStatusEnum.Completed : ResumeStatusEnum.Pending
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error uploading resume for JobId: {jobId}, CampaignId: {campaignId}");
                Console.WriteLine($"📋 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"🔴 Exception Message: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                
                // Log inner exception if exists
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"💥 Inner Exception Type: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"💥 Inner Exception Message: {ex.InnerException.Message}");
                    Console.WriteLine($"🔍 Inner Stack trace: {ex.InnerException.StackTrace}");
                    
                    // Check for PostgreSQL specific errors
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"🔥 Deepest Exception: {ex.InnerException.InnerException.Message}");
                    }
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
                    companyId = job.CompanyId,
                    resumeId = resume.ResumeId,
                    applicationId = resumeApplication.ApplicationId,
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
                    // Keep Redis mode string in sync with ProcessingModeEnum
                    mode = ProcessingModeEnum.Rescore.ToString().ToLowerInvariant(),
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
                    // Update application/resume: new queueJobId and status = Pending
                    resumeApplication.QueueJobId = newQueueJobId;
                    resumeApplication.Status = ApplicationStatusEnum.Pending;
                    resumeApplication.ProcessingMode = ProcessingModeEnum.Rescore;
                    resumeApplication.ProcessedAt = null; // Reset, will be set when AI finishes
                    resumeApplication.ProcessingTimeMs = null;
                    resume.Status = ResumeStatusEnum.Pending;
                    await _resumeApplicationRepo.UpdateAsync(resumeApplication);
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var rescoreResume = await resumeRepo.GetByJobIdAndResumeIdAsync(jobId, resume.ResumeId);
                    
                    // Capture data for SignalR BEFORE background task
                    var jobIdForSignalR = jobId;
                    var resumeIdForSignalR = resume.ResumeId;
                    
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "rescore_initiated", 
                            new { newStatus = "Pending" }, rescoreResume, null);
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
                
                // 1. Find ResumeApplication by queueJobId (includes Resume)
                var resumeApplication = await _resumeApplicationRepo.GetByQueueJobIdWithDetailsAsync(request.QueueJobId);
                if (resumeApplication == null || resumeApplication.Resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found for the given queue job ID."
                    };
                }

                var resume = resumeApplication.Resume;

                // Validate applicationId if provided
                if (request.ApplicationId.HasValue && request.ApplicationId.Value != resumeApplication.ApplicationId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Application ID does not match the queue job."
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
                        resume.Data = null; // keep Data null on error
                        
                        // Update ResumeApplication status to Failed for resume errors
                        resumeApplication.Status = ApplicationStatusEnum.Failed;
                        resumeApplication.ErrorMessage = errorMessage;
                        await _resumeApplicationRepo.UpdateAsync(resumeApplication);

                        await resumeRepo.UpdateAsync(resume);
                        await _uow.CommitTransactionAsync();

                        // Reload resume for SignalR
                        var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                        
                        // Capture data for SignalR BEFORE background task
                        var jobIdForSignalR = resumeApplication.JobId;
                        var resumeIdForSignalR = resume.ResumeId;
                        var errorStatusStr = errorStatus.ToString();
                        
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                                new { newStatus = errorStatusStr }, resumeForSignalR, null);
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
                    var fullName = request.CandidateInfo?.FullName;
                    var email = request.CandidateInfo?.Email;
                    var phone = request.CandidateInfo?.PhoneNumber;
                    
                    // Get CompanyId from Job
                    var companyId = resumeApplication.Job?.CompanyId;
                    if (!companyId.HasValue)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "Company ID not found for the job."
                        };
                    }

                    // Check for duplicate candidate in the company by email, fullName (case-insensitive), or phone
                    var duplicateCandidate = await candidateRepo.FindDuplicateCandidateInCompanyAsync(
                        companyId.Value,
                        email,
                        fullName,
                        phone
                    );

                    Candidate candidate;
                    
                    // If duplicate found, use that candidate (DO NOT update info to preserve original)
                    if (duplicateCandidate != null)
                    {
                        candidate = duplicateCandidate;
                        
                        // ✅ Link resume to existing candidate WITHOUT updating candidate info
                        // This preserves the original candidate's information and prevents 
                        // overwriting when multiple resumes share the same email/phone
                        resume.CandidateId = candidate.CandidateId;
                        resumeApplication.CandidateId = candidate.CandidateId;
                    }
                    else
                    {
                        // Check if candidate is already loaded from includes to avoid tracking conflicts
                        var existingCandidate = resume.Candidate;
                        
                        // If not loaded, query it
                        if (existingCandidate == null && resume.CandidateId.HasValue)
                        {
                            existingCandidate = await candidateRepo.GetByResumeIdAsync(resume.ResumeId);
                        }

                        if (existingCandidate == null)
                        {
                            // Create new candidate
                            candidate = new Candidate
                            {
                                FullName = fullName,
                                Email = email,
                                PhoneNumber = phone
                            };

                            await candidateRepo.CreateAsync(candidate);
                            await _uow.SaveChangesAsync(); // Get CandidateId
                            
                            // Link resume to candidate
                            resume.CandidateId = candidate.CandidateId;
                        }
                        else
                        {
                            // ✅ Use existing candidate WITHOUT updating info
                            // Resume already has a candidate, keep the original candidate data
                            candidate = existingCandidate;
                            
                            // Link resume to candidate (should already be linked)
                            resume.CandidateId = candidate.CandidateId;
                            resumeApplication.CandidateId = candidate.CandidateId;
                        }
                    }

                    // 5. Save AI Score to ResumeApplication (not Resume)
                    string? aiExplanationString = request.AIExplanation != null
                        ? JsonUtils.SerializeRawJsonSafe(request.AIExplanation)
                        : null;

                    // Calculate processing time (from creation to now)
                    var processingTimeMs = resumeApplication.CreatedAt.HasValue
                        ? (int)(DateTime.UtcNow - resumeApplication.CreatedAt.Value).TotalMilliseconds
                        : (int?)null;

                    // Update ResumeApplication with scores
                    resumeApplication.CandidateId = candidate.CandidateId;
                    resumeApplication.TotalScore = request.TotalResumeScore.Value;
                    resumeApplication.Status = ApplicationStatusEnum.Reviewed;
                    resumeApplication.AIExplanation = aiExplanationString;
                    resumeApplication.RequiredSkills = request.RequiredSkills;
                    resumeApplication.MatchSkills = request.CandidateInfo?.MatchSkills;
                    resumeApplication.MissingSkills = request.CandidateInfo?.MissingSkills;
                    resumeApplication.ProcessedAt = DateTime.UtcNow;
                    resumeApplication.ProcessingTimeMs = processingTimeMs;
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
                    
                    // Capture data for SignalR BEFORE background task (to avoid disposed DbContext)
                    var jobIdForSignalR = resumeApplication.JobId;
                    var resumeIdForSignalR = resume.ResumeId;
                    var applicationForSignalR = new ResumeApplication
                    {
                        TotalScore = resumeApplication.TotalScore,
                        AdjustedScore = resumeApplication.AdjustedScore
                    };
                    
                    // Send real-time SignalR update (fire and forget, but with proper data)
                    _ = Task.Run(async () => 
                    {
                        // Small delay to ensure all changes are persisted
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                            new { newStatus = "Completed" }, resumeForSignalR, applicationForSignalR);
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
                    var failedApplication = await _resumeApplicationRepo.GetByQueueJobIdWithDetailsAsync(request.QueueJobId);
                    if (failedApplication?.Resume != null)
                    {
                        var parsedResume = failedApplication.Resume;
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            parsedResume.Status = ResumeStatusEnum.Failed;
                            var resumeRepoInner = _uow.GetRepository<IResumeRepository>();
                            await resumeRepoInner.UpdateAsync(parsedResume);
                            await _uow.CommitTransactionAsync();

                            if (failedApplication != null)
                            {
                                // Reload resume for SignalR
                                var resumeForSignalR = await resumeRepoInner.GetByJobIdAndResumeIdAsync(failedApplication.JobId, parsedResume.ResumeId);
                                
                                // Capture data for SignalR BEFORE background task
                                var jobIdForSignalR = failedApplication.JobId;
                                var resumeIdForSignalR = parsedResume.ResumeId;
                                
                                // Send real-time SignalR update
                                _ = Task.Run(async () => 
                                {
                                    await Task.Delay(200);
                                    await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                                        new { newStatus = "Failed" }, resumeForSignalR, null);
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
                        AdjustedScore = application.AdjustedScore,
                        Note = application.Note
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
                    QueueJobId = resumeApplication?.QueueJobId ?? string.Empty,
                    FileUrl = resume?.FileUrl ?? string.Empty,
                    OriginalFileName = resume?.OriginalFileName,
                    Status = resume?.Status ?? ResumeStatusEnum.Pending,
                    ApplicationStatus = resumeApplication?.Status ?? ApplicationStatusEnum.Pending,
                    CampaignId = resumeApplication?.CampaignId,
                    CreatedAt = resume?.CreatedAt,
                    CandidateId = resumeApplication?.CandidateId ?? candidate?.CandidateId ?? 0,
                    FullName = candidate?.FullName ?? "Unknown",
                    Email = candidate?.Email ?? "N/A",
                    PhoneNumber = candidate?.PhoneNumber,
                    MatchSkills = resumeApplication?.MatchSkills,
                    MissingSkills = resumeApplication?.MissingSkills,
                    TotalScore = resumeApplication?.TotalScore,
                    AdjustedScore = resumeApplication?.AdjustedScore,
                    AIExplanation = resumeApplication?.AIExplanation,
                    ErrorMessage = resumeApplication?.ErrorMessage,
                    Note = resumeApplication?.Note,
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
                    companyId = job.CompanyId,
                    resumeId = resume.ResumeId,
                    applicationId = resumeApplication.ApplicationId,
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
                    // Keep Redis mode string in sync with ProcessingModeEnum
                    mode = ProcessingModeEnum.Parse.ToString().ToLowerInvariant()
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
                    // Update application/resume: new queueJobId and status = Pending
                    resumeApplication.QueueJobId = newQueueJobId;
                    resumeApplication.Status = ApplicationStatusEnum.Pending;
                    resumeApplication.ProcessingMode = ProcessingModeEnum.Parse; // Retry = parse again
                    resumeApplication.ProcessedAt = null; // Reset, will be set when AI finishes
                    resumeApplication.ProcessingTimeMs = null;
                    resume.Status = ResumeStatusEnum.Pending;
                    await _resumeApplicationRepo.UpdateAsync(resumeApplication);
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var retryResume = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                    
                    // Capture data for SignalR BEFORE background task
                    var jobIdForSignalR = resumeApplication.JobId;
                    var resumeIdForSignalR = resume.ResumeId;
                    
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "retried", 
                            new { newStatus = "Pending" }, retryResume, null);
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

        public async Task<ServiceResponse> SoftDeleteResumeAsync(int applicationId)
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
                var jobRepo = _uow.GetRepository<IJobRepository>();
                
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get ResumeApplication with details for validation
                var resumeApplication = await _resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(applicationId);
                if (resumeApplication == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found."
                    };
                }

                // Validate company ownership through Job
                var job = await jobRepo.GetJobByIdAsync(resumeApplication.JobId);
                if (job == null || job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to delete this resume application."
                    };
                }

                // Check if already deleted
                if (!resumeApplication.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Resume application is already deleted."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Soft delete ResumeApplication: set IsActive = false
                    resumeApplication.IsActive = false;
                    await _resumeApplicationRepo.UpdateAsync(resumeApplication);
                    await _uow.CommitTransactionAsync();

                    // Get resume for SignalR update
                    var resume = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resumeApplication.ResumeId);

                    // Capture data for SignalR BEFORE background task
                    var jobIdForSignalR = resumeApplication.JobId;
                    var resumeIdForSignalR = resumeApplication.ResumeId;

                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "deleted", null, resume, null);
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
                    Message = "Resume application deleted successfully.",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting resume application: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting the resume application."
                };
            }
        }


        /// <summary>
        /// Send real-time SignalR update to clients watching a job's resumes
        /// Note: All data should be loaded BEFORE calling this in a background task to avoid DbContext disposal issues
        /// </summary>
        private async Task SendResumeUpdateAsync(int jobId, int resumeId, string eventType, object? data = null, Resume? resume = null, ResumeApplication? resumeApplication = null)
        {
            try
            {
                if (resume == null)
                {
                    Console.WriteLine($"⚠️ SignalR: Resume data not provided for resume {resumeId}, cannot send update");
                    return;
                }

                var updateData = new
                {
                    eventType, // "uploaded", "cloned", "status_changed", "deleted", "retried"
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

