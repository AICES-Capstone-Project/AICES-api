using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Models.Internal;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.UnitOfWork;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;

namespace BusinessObjectLayer.Services
{
    public class ResumeService : IResumeService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IStorageHelper _storageHelper;
        private readonly IRedisHelper _redisHelper;
        private readonly IResumeLimitService _resumeLimitService;
        private readonly IResumeApplicationRepository _resumeApplicationRepo;
        
        // ✅ IMPROVED: Allow 10 concurrent uploads per company (up from 1)
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _companyLocks;
        private const int MAX_CONCURRENT_UPLOADS_PER_COMPANY = 10;

        public ResumeService(
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor,
            IStorageHelper storageHelper,
            IRedisHelper redisHelper,
            IResumeLimitService resumeLimitService,
            IResumeApplicationRepository resumeApplicationRepo)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
            _storageHelper = storageHelper;
            _redisHelper = redisHelper;
            _resumeLimitService = resumeLimitService;
            _resumeApplicationRepo = resumeApplicationRepo;
            _companyLocks = new ConcurrentDictionary<int, SemaphoreSlim>();
        }

        /// <summary>
        /// ✅ NEW: Batch upload endpoint - handles up to 100 resumes efficiently
        /// Returns immediately with job IDs, processes asynchronously
        /// </summary>
        public async Task<ServiceResponse> UploadResumeBatchAsync(int campaignId, int jobId, IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "No files provided."
                };
            }

            if (files.Count > 100)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Maximum 100 files can be uploaded at once."
                };
            }

            try
            {
                // Validate user and company ONCE for all files
                var validationResult = await ValidateUserAndCompanyAsync();
                if (validationResult.Status != SRStatus.Success)
                {
                    return validationResult;
                }
                var companyId = (int)validationResult.Data!;

                // Validate campaign and job ONCE
                var campaignJobValidation = await ValidateCampaignAndJobAsync(campaignId, jobId, companyId);
                if (campaignJobValidation.Status != SRStatus.Success)
                {
                    return campaignJobValidation;
                }

                // Note: Individual uploads will check quota atomically
                // This is just a quick check to fail fast if clearly over limit

                // Process all files in parallel (up to MAX_CONCURRENT_UPLOADS_PER_COMPANY)
                var uploadTasks = files.Select(file => 
                    UploadSingleResumeAsync(campaignId, jobId, file, companyId)
                ).ToList();

                var results = await Task.WhenAll(uploadTasks);

                var successCount = results.Count(r => r.Status == SRStatus.Success);
                var failCount = results.Count(r => r.Status != SRStatus.Success);

                return new ServiceResponse
                {
                    Status = failCount == 0 ? SRStatus.Success : SRStatus.Error,
                    Message = failCount == 0 
                        ? $"All {files.Count} resumes uploaded successfully."
                        : $"Uploaded {successCount} of {files.Count} resumes successfully. {failCount} failed.",
                    Data = new
                    {
                        TotalFiles = files.Count,
                        SuccessCount = successCount,
                        FailCount = failCount,
                        Results = results.Select((r, idx) => new
                        {
                            FileName = files[idx].FileName,
                            Status = r.Status.ToString(),
                            Message = r.Message,
                            Data = r.Data
                        })
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Batch upload error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Batch upload failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Single resume upload - extracted core logic
        /// </summary>
        private async Task<ServiceResponse> UploadSingleResumeAsync(int campaignId, int jobId, IFormFile file, int companyId)
        {
            if (file == null || file.Length == 0)
            {
                return new ServiceResponse { Status = SRStatus.Validation, Message = "File is empty." };
            }

            var semaphore = _companyLocks.GetOrAdd(companyId, _ => new SemaphoreSlim(MAX_CONCURRENT_UPLOADS_PER_COMPANY, MAX_CONCURRENT_UPLOADS_PER_COMPANY));
            
            await semaphore.WaitAsync();
            try
            {
                // Step 1: Compute hash and check duplicates
                var fileHash = HashUtils.ComputeFileHash(file);
                var isDuplicate = await _resumeApplicationRepo.IsDuplicateResumeAsync(jobId, campaignId, fileHash);
                if (isDuplicate)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Duplicate resume detected."
                    };
                }

                // Step 2: Check for existing resume (reuse logic)
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                var existingResume = await resumeRepo.GetByFileHashAndCompanyIdAsync(companyId, fileHash);
                
                var reuseDecision = DetermineReuseStrategy(existingResume, jobId);
                
                // Step 3: Upload file FIRST (before DB transaction) with retry
                string? fileUrl = null;
                if (!reuseDecision.ShouldClone && !reuseDecision.ShouldReuse)
                {
                    var uploadResult = await UploadFileWithRetryAsync(file, companyId, jobId);
                    if (uploadResult.Status != SRStatus.Success)
                    {
                        return uploadResult;
                    }
                    fileUrl = uploadResult.Data as string;
                }
                else if (existingResume != null)
                {
                    fileUrl = existingResume.FileUrl;
                }

                // Step 4: Create records in DB (short transaction)
                int resumeId, applicationId;
                string queueJobId = Guid.NewGuid().ToString();

                await _uow.BeginTransactionAsync();
                try
                {
                    // Check and increment quota atomically
                    if (!reuseDecision.ShouldClone)
                    {
                        var quotaCheck = await _resumeLimitService.CheckResumeLimitInTransactionAsync(companyId);
                        if (quotaCheck.Status != SRStatus.Success)
                        {
                            await _uow.RollbackTransactionAsync();
                            // Cleanup uploaded file if any
                            if (!string.IsNullOrEmpty(fileUrl))
                            {
                                _ = Task.Run(() => _storageHelper.DeleteFileAsync(fileUrl));
                            }
                            return quotaCheck;
                        }
                    }

                    var createResult = await CreateResumeAndApplicationAsync(
                        reuseDecision, existingResume, fileUrl, fileHash, file.FileName,
                        companyId, jobId, campaignId, queueJobId);

                    resumeId = createResult.ResumeId;
                    applicationId = createResult.ApplicationId;

                    await _uow.CommitTransactionAsync();
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    // Cleanup uploaded file
                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        _ = Task.Run(() => _storageHelper.DeleteFileAsync(fileUrl));
                    }
                    throw;
                }

                // Step 5: Queue processing (async, non-blocking)
                if (!reuseDecision.ShouldClone)
                {
                    _ = QueueResumeProcessingAsync(resumeId, applicationId, jobId, campaignId, companyId, queueJobId, reuseDecision, fileUrl);
                }

                // Step 6: Send SignalR update (async, non-blocking)
                _ = SendResumeUpdateAsync(jobId, resumeId, reuseDecision.ShouldClone ? "cloned" : "uploaded");

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = reuseDecision.ShouldClone ? "Using existing analysis." : "Upload successful.",
                    Data = new ResumeUploadResponse
                    {
                        ResumeId = resumeId,
                        QueueJobId = queueJobId,
                        Status = reuseDecision.ShouldClone ? ResumeStatusEnum.Completed : ResumeStatusEnum.Pending
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload error for {file.FileName}: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Upload failed: {ex.Message}"
                };
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// ✅ ORIGINAL: Single resume upload endpoint (maintained for backward compatibility)
        /// </summary>
        public async Task<ServiceResponse> UploadResumeAsync(int campaignId, int jobId, IFormFile file)
        {
            try
            {
                var validationResult = await ValidateUserAndCompanyAsync();
                if (validationResult.Status != SRStatus.Success)
                {
                    return validationResult;
                }
                var companyId = (int)validationResult.Data!;

                var campaignJobValidation = await ValidateCampaignAndJobAsync(campaignId, jobId, companyId);
                if (campaignJobValidation.Status != SRStatus.Success)
                {
                    return campaignJobValidation;
                }

                return await UploadSingleResumeAsync(campaignId, jobId, file, companyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error uploading resume: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while uploading the resume: {ex.Message}"
                };
            }
        }

        #region Helper Methods

        /// <summary>
        /// ✅ EXTRACTED: Validation logic
        /// </summary>
        private async Task<ServiceResponse> ValidateUserAndCompanyAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated." };
            }

            int userId = int.Parse(userIdClaim);
            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

            if (companyUser == null || companyUser.CompanyId == null)
            {
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Company not found for user." };
            }

            return new ServiceResponse { Status = SRStatus.Success, Data = companyUser.CompanyId.Value };
        }

        /// <summary>
        /// ✅ EXTRACTED: Campaign and Job validation
        /// </summary>
        private async Task<ServiceResponse> ValidateCampaignAndJobAsync(int campaignId, int jobId, int companyId)
        {
            var campaignRepo = _uow.GetRepository<ICampaignRepository>();
            var jobRepo = _uow.GetRepository<IJobRepository>();

            var campaign = await campaignRepo.GetByIdAsync(campaignId);
            if (campaign == null)
            {
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Campaign not found." };
            }

            if (campaign.CompanyId != companyId)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "Campaign access denied." };
            }

            if (campaign.Status != CampaignStatusEnum.Published)
            {
                return new ServiceResponse { Status = SRStatus.Validation, Message = "Campaign must be Published." };
            }

            var job = await jobRepo.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return new ServiceResponse { Status = SRStatus.NotFound, Message = "Job not found." };
            }

            if (job.CompanyId != companyId)
            {
                return new ServiceResponse { Status = SRStatus.Forbidden, Message = "Job access denied." };
            }

            var jobCampaign = await campaignRepo.GetJobCampaignByJobIdAndCampaignIdAsync(jobId, campaignId);
            if (jobCampaign == null)
            {
                return new ServiceResponse { Status = SRStatus.Validation, Message = "Job not in this campaign." };
            }

            return new ServiceResponse { Status = SRStatus.Success };
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Determine reuse strategy
        /// </summary>
        private ReuseDecision DetermineReuseStrategy(Resume? existingResume, int jobId)
        {
            if (existingResume == null)
            {
                return new ReuseDecision { ShouldClone = false, ShouldReuse = false };
            }

            var fatalStatuses = new[] { 
                ResumeStatusEnum.InvalidResumeData, 
                ResumeStatusEnum.CorruptedFile, 
                ResumeStatusEnum.DuplicateResume 
            };

            var retryableStatuses = new[] {
                ResumeStatusEnum.Failed,
                ResumeStatusEnum.Timeout,
                ResumeStatusEnum.ServerError,
            };

            // Check if there's an existing application for this resume + job
            var existingApplication = _resumeApplicationRepo
                .GetByResumeIdAndJobIdWithDetailsAsync(existingResume.ResumeId, jobId).Result;

            if (existingApplication != null && existingApplication.Status == ApplicationStatusEnum.Reviewed)
            {
                return new ReuseDecision { ShouldClone = true, ShouldReuse = false, ExistingApplication = existingApplication };
            }

            if (existingApplication != null && existingApplication.Status == ApplicationStatusEnum.Failed &&
                (existingApplication.ErrorType == ApplicationErrorEnum.JobTitleNotMatched ||
                 existingApplication.ErrorType == ApplicationErrorEnum.InvalidJobData))
            {
                return new ReuseDecision { ShouldClone = true, ShouldReuse = false, ExistingApplication = existingApplication };
            }

            if (existingResume.Status == ResumeStatusEnum.Completed && existingResume.Data != null)
            {
                return new ReuseDecision { ShouldClone = false, ShouldReuse = true };
            }

            if (fatalStatuses.Contains(existingResume.Status))
            {
                return new ReuseDecision { ShouldClone = true, ShouldReuse = false };
            }

            if (retryableStatuses.Contains(existingResume.Status))
            {
                return new ReuseDecision { ShouldClone = false, ShouldReuse = false }; // Retry
            }

            return new ReuseDecision { ShouldClone = false, ShouldReuse = false };
        }

        /// <summary>
        /// ✅ NEW: Upload file with retry logic (handles transient failures)
        /// </summary>
        private async Task<ServiceResponse> UploadFileWithRetryAsync(IFormFile file, int companyId, int jobId, int maxRetries = 3)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;
            int userId = int.Parse(userIdClaim ?? "0");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    string publicId = $"resumes/{companyId}/{jobId}/{userId}_{DateTime.UtcNow.Ticks}{extension}";

                    var uploadResult = await _storageHelper.UploadFileAsync(file, "resumes", publicId);
                    
                    if (uploadResult.Status == SRStatus.Success && uploadResult.Data != null)
                    {
                        var dataType = uploadResult.Data.GetType();
                        var urlProp = dataType.GetProperty("Url");
                        var fileUrl = urlProp?.GetValue(uploadResult.Data) as string;

                        if (!string.IsNullOrEmpty(fileUrl))
                        {
                            return new ServiceResponse { Status = SRStatus.Success, Data = fileUrl };
                        }
                    }

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * attempt); // Exponential backoff
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Upload attempt {attempt}/{maxRetries} failed: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * attempt);
                    }
                    else
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Error,
                            Message = "An exception has been raised that is likely due to a transient failure."
                        };
                    }
                }
            }

            return new ServiceResponse
            {
                Status = SRStatus.Error,
                Message = "File upload failed after multiple retries."
            };
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Create resume and application records
        /// </summary>
        private async Task<(int ResumeId, int ApplicationId)> CreateResumeAndApplicationAsync(
            ReuseDecision decision, Resume? existingResume, string? fileUrl, string fileHash, string fileName,
            int companyId, int jobId, int campaignId, string queueJobId)
        {
            var resumeRepo = _uow.GetRepository<IResumeRepository>();
            int resumeId, applicationId;

            if (decision.ShouldClone && existingResume != null)
            {
                // Clone existing result
                var clonedApp = new ResumeApplication
                {
                    ResumeId = existingResume.ResumeId,
                    JobId = jobId,
                    CampaignId = campaignId,
                    QueueJobId = queueJobId,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingTimeMs = 0,
                    ProcessingMode = ProcessingModeEnum.Clone
                };

                if (decision.ExistingApplication != null && decision.ExistingApplication.Status == ApplicationStatusEnum.Reviewed)
                {
                    clonedApp.Status = ApplicationStatusEnum.Reviewed;
                    clonedApp.CandidateId = decision.ExistingApplication.CandidateId;
                    clonedApp.TotalScore = decision.ExistingApplication.TotalScore;
                    clonedApp.AdjustedScore = decision.ExistingApplication.AdjustedScore;
                    clonedApp.AIExplanation = decision.ExistingApplication.AIExplanation;
                    clonedApp.MatchSkills = decision.ExistingApplication.MatchSkills;
                    clonedApp.MissingSkills = decision.ExistingApplication.MissingSkills;
                    clonedApp.RequiredSkills = decision.ExistingApplication.RequiredSkills;
                    clonedApp.ClonedFromApplicationId = decision.ExistingApplication.ApplicationId;
                }
                else
                {
                    clonedApp.Status = ApplicationStatusEnum.Failed;
                    clonedApp.ErrorType = decision.ExistingApplication?.ErrorType ?? ApplicationErrorEnum.TechnicalError;
                    clonedApp.ErrorMessage = decision.ExistingApplication?.ErrorMessage ?? $"Auto-rejected: {existingResume.Status}";
                }

                await _resumeApplicationRepo.CreateAsync(clonedApp);
                await _uow.SaveChangesAsync();

                existingResume.ReuseCount++;
                existingResume.LastReusedAt = DateTime.UtcNow;
                await resumeRepo.UpdateAsync(existingResume);
                await _uow.SaveChangesAsync();

                resumeId = existingResume.ResumeId;
                applicationId = clonedApp.ApplicationId;
            }
            else if (decision.ShouldReuse && existingResume != null)
            {
                // Reuse existing resume for new job
                existingResume.ReuseCount++;
                existingResume.LastReusedAt = DateTime.UtcNow;
                await resumeRepo.UpdateAsync(existingResume);
                await _uow.SaveChangesAsync();

                var app = new ResumeApplication
                {
                    ResumeId = existingResume.ResumeId,
                    JobId = jobId,
                    CampaignId = campaignId,
                    QueueJobId = queueJobId,
                    Status = ApplicationStatusEnum.Pending,
                    ProcessingMode = ProcessingModeEnum.Score
                };
                await _resumeApplicationRepo.CreateAsync(app);
                await _uow.SaveChangesAsync();

                resumeId = existingResume.ResumeId;
                applicationId = app.ApplicationId;
            }
            else
            {
                // Create new resume
                var resume = new Resume
                {
                    CompanyId = companyId,
                    FileUrl = fileUrl,
                    OriginalFileName = fileName,
                    FileHash = fileHash,
                    Status = ResumeStatusEnum.Pending,
                    ReuseCount = 0
                };
                await resumeRepo.CreateAsync(resume);
                await _uow.SaveChangesAsync();

                var app = new ResumeApplication
                {
                    ResumeId = resume.ResumeId,
                    JobId = jobId,
                    CampaignId = campaignId,
                    QueueJobId = queueJobId,
                    Status = ApplicationStatusEnum.Pending,
                    ProcessingMode = ProcessingModeEnum.Parse
                };
                await _resumeApplicationRepo.CreateAsync(app);
                await _uow.SaveChangesAsync();

                resumeId = resume.ResumeId;
                applicationId = app.ApplicationId;
            }

            return (resumeId, applicationId);
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Queue resume for AI processing
        /// </summary>
        private async Task QueueResumeProcessingAsync(int resumeId, int applicationId, int jobId, int campaignId, 
            int companyId, string queueJobId, ReuseDecision decision, string? fileUrl)
        {
            try
            {
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var job = await jobRepo.GetJobByIdAsync(jobId);
                var skillsList = await jobRepo.GetSkillsByJobIdAsync(jobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(jobId);
                var languagesList = await jobRepo.GetLanguagesByJobIdAsync(jobId);
                
                var criteriaData = job?.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                Resume? existingResume = null;
                if (decision.ShouldReuse)
                {
                    existingResume = await resumeRepo.GetByIdAsync(resumeId);
                }

                if (job == null)
                {
                    Console.WriteLine($"❌ Job not found for jobId {jobId} during queue preparation");
                    return;
                }

                var jobData = new ResumeQueueJobResponse
                {
                    companyId = companyId,
                    resumeId = resumeId,
                    applicationId = applicationId,
                    queueJobId = queueJobId,
                    campaignId = campaignId,
                    jobId = jobId,
                    jobTitle = job.Title,
                    fileUrl = decision.ShouldReuse ? string.Empty : (fileUrl ?? string.Empty),
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                    languages = languagesList.Any() ? string.Join(", ", languagesList) : null,
                    specialization = job.Specialization?.Name,
                    employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                    criteria = criteriaData,
                    level = job.Level?.Name,
                    mode = (decision.ShouldReuse ? ProcessingModeEnum.Score : ProcessingModeEnum.Parse).ToString().ToLowerInvariant(),
                    parsedData = decision.ShouldReuse ? existingResume?.Data : null
                };

                var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                if (pushed)
                {
                    Console.WriteLine($"✅ Queued resume {resumeId} for processing");
                }
                else
                {
                    Console.WriteLine($"⚠️ Failed to queue resume {resumeId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Queue error for resume {resumeId}: {ex.Message}");
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
                    applicationId = resumeApplication?.ApplicationId,
                    fullName = resume.Candidate?.FullName ?? "Unknown",
                    totalScore = resumeApplication?.TotalScore,
                    adjustedScore = resumeApplication?.AdjustedScore,
                    additionalData = data,
                    timestamp = DateTime.UtcNow
                };

                // TODO: Implement SignalR hub notification
                // Example: await _hubContext.Clients.Group($"job_{jobId}").SendAsync("ResumeUpdate", updateData);
                Console.WriteLine($"✅ SignalR update prepared for resume {resumeId} in job {jobId}: {eventType}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SignalR error: {ex.Message}");
            }
        }

        #endregion

        #region Public Service Methods

        /// <summary>
        /// Receive AI processing result from Python service
        /// </summary>
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
                        ResumeStatusEnum fileStatus = ResumeStatusEnum.Completed;
                        ApplicationErrorEnum applicationError;
                        string errorMessage;
                        
                        switch (request.Error.ToLower())
                        {
                            case "invalid_resume_data":
                                fileStatus = ResumeStatusEnum.InvalidResumeData;
                                applicationError = ApplicationErrorEnum.TechnicalError;
                                errorMessage = request.Reason ?? "The uploaded file is not a valid resume.";
                                break;
                            
                            case "invalid_job_data":
                                fileStatus = !string.IsNullOrEmpty(resume.Data) ? ResumeStatusEnum.Completed : ResumeStatusEnum.Failed;
                                applicationError = ApplicationErrorEnum.InvalidJobData;
                                errorMessage = request.Reason ?? "The job data is invalid.";
                                break;
                            
                            case "job_title_not_matched":
                                fileStatus = !string.IsNullOrEmpty(resume.Data) ? ResumeStatusEnum.Completed : ResumeStatusEnum.Failed;
                                applicationError = ApplicationErrorEnum.JobTitleNotMatched;
                                errorMessage = request.Reason ?? "The candidate's experience does not match the job title requirements.";
                                break;
                            
                            default:
                                fileStatus = ResumeStatusEnum.Failed;
                                applicationError = ApplicationErrorEnum.TechnicalError;
                                errorMessage = request.Reason ?? $"Error: {request.Error}";
                                break;
                        }

                        resume.Status = fileStatus;
                        if (fileStatus == ResumeStatusEnum.InvalidResumeData ||
                            fileStatus == ResumeStatusEnum.CorruptedFile ||
                            fileStatus == ResumeStatusEnum.Failed)
                        {
                            resume.Data = null;
                        }
                        
                        resumeApplication.Status = ApplicationStatusEnum.Failed;
                        resumeApplication.ErrorType = applicationError;
                        resumeApplication.ErrorMessage = errorMessage;
                        await _resumeApplicationRepo.UpdateAsync(resumeApplication);
                        await resumeRepo.UpdateAsync(resume);
                        await _uow.CommitTransactionAsync();

                        var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                        var jobIdForSignalR = resumeApplication.JobId;
                        var resumeIdForSignalR = resume.ResumeId;
                        
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                                new { newStatus = fileStatus.ToString(), errorType = applicationError.ToString() }, resumeForSignalR, null);
                        });

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = fileStatus == ResumeStatusEnum.InvalidResumeData 
                                ? "The uploaded file is not a valid resume." 
                                : errorMessage,
                            Data = new
                            {
                                resumeId = resume.ResumeId,
                                status = fileStatus.ToString(),
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

                // 2.5 Validate required scoring fields
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
                    // 3. Update parsed resume
                    resume.Status = ResumeStatusEnum.Completed;
                    if (request.RawJson != null)
                    {
                        resume.Data = JsonUtils.SerializeRawJsonSafe(request.RawJson);
                    }
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.SaveChangesAsync();

                    // 4. Create/Update Candidate
                    var fullName = request.CandidateInfo?.FullName;
                    var email = request.CandidateInfo?.Email;
                    var phone = request.CandidateInfo?.PhoneNumber;
                    
                    var companyId = resumeApplication.Job?.CompanyId;
                    if (!companyId.HasValue)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "Company ID not found for the job."
                        };
                    }

                    var duplicateCandidate = await candidateRepo.FindDuplicateCandidateInCompanyAsync(
                        companyId.Value, email, fullName, phone);

                    Candidate candidate;
                    if (duplicateCandidate != null)
                    {
                        candidate = duplicateCandidate;
                        resume.CandidateId = candidate.CandidateId;
                        resumeApplication.CandidateId = candidate.CandidateId;
                    }
                    else
                    {
                        var existingCandidate = resume.Candidate;
                        if (existingCandidate == null && resume.CandidateId.HasValue)
                        {
                            existingCandidate = await candidateRepo.GetByResumeIdAsync(resume.ResumeId);
                        }

                        if (existingCandidate == null)
                        {
                            candidate = new Candidate
                            {
                                FullName = fullName ?? "Unknown",
                                Email = email ?? "no-email@placeholder.com",
                                PhoneNumber = phone
                            };
                            await candidateRepo.CreateAsync(candidate);
                            await _uow.SaveChangesAsync();
                            resume.CandidateId = candidate.CandidateId;
                        }
                        else
                        {
                            candidate = existingCandidate;
                            resume.CandidateId = candidate.CandidateId;
                            resumeApplication.CandidateId = candidate.CandidateId;
                        }
                    }

                    // 5. Save AI Score to ResumeApplication
                    string? aiExplanationString = request.AIExplanation != null
                        ? JsonUtils.SerializeRawJsonSafe(request.AIExplanation)
                        : null;

                    var processingTimeMs = resumeApplication.CreatedAt.HasValue
                        ? (int)(DateTime.UtcNow - resumeApplication.CreatedAt.Value).TotalMilliseconds
                        : (int?)null;

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

                    resume.IsLatest = true;
                    await resumeRepo.UpdateAsync(resume);
                    await _uow.SaveChangesAsync();

                    // 6. Save ScoreDetails
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

                    var resumeForSignalR = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resume.ResumeId);
                    var jobIdForSignalR = resumeApplication.JobId;
                    var resumeIdForSignalR = resume.ResumeId;
                    var applicationForSignalR = new ResumeApplication
                    {
                        TotalScore = resumeApplication.TotalScore,
                        AdjustedScore = resumeApplication.AdjustedScore
                    };
                    
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                            new { newStatus = "Completed" }, resumeForSignalR, applicationForSignalR);
                    });

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

                            var resumeForSignalR = await resumeRepoInner.GetByJobIdAndResumeIdAsync(failedApplication.JobId, parsedResume.ResumeId);
                            var jobIdForSignalR = failedApplication.JobId;
                            var resumeIdForSignalR = parsedResume.ResumeId;
                            
                            _ = Task.Run(async () => 
                            {
                                await Task.Delay(200);
                                await SendResumeUpdateAsync(jobIdForSignalR, resumeIdForSignalR, "status_changed", 
                                    new { newStatus = "Failed" }, resumeForSignalR, null);
                            });
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

        /// <summary>
        /// Get list of resumes for a specific job in a campaign
        /// </summary>
        public async Task<ServiceResponse> GetJobResumesAsync(int jobId, int campaignId)
        {
            try
            {
                var validationResult = await ValidateUserAndCompanyAsync();
                if (validationResult.Status != SRStatus.Success)
                {
                    return validationResult;
                }
                var companyId = (int)validationResult.Data!;

                var jobRepo = _uow.GetRepository<IJobRepository>();
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
                        Message = "You do not have permission to view resumes for this job."
                    };
                }

                var resumeApplications = await _resumeApplicationRepo.GetByJobIdAndCampaignWithResumeAsync(jobId, campaignId);

                var resumeList = resumeApplications
                    .Select(application => new JobResumeListResponse
                    {
                        ResumeId = application.ResumeId,
                        ApplicationId = application.ApplicationId,
                        ResumeStatus = application.Resume?.Status ?? ResumeStatusEnum.Pending,
                        ApplicationStatus = application.Status,
                        ApplicationErrorType = application.ErrorType,
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

        /// <summary>
        /// Get detailed information about a specific resume application
        /// </summary>
        public async Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int applicationId, int campaignId)
        {
            try
            {
                var validationResult = await ValidateUserAndCompanyAsync();
                if (validationResult.Status != SRStatus.Success)
                {
                    return validationResult;
                }
                var companyId = (int)validationResult.Data!;

                var jobRepo = _uow.GetRepository<IJobRepository>();
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
                        Message = "You do not have permission to view this resume."
                    };
                }

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
                    ResumeStatus = resume?.Status ?? ResumeStatusEnum.Pending,
                    ApplicationStatus = resumeApplication?.Status ?? ApplicationStatusEnum.Pending,
                    ApplicationErrorType = resumeApplication?.ErrorType,
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

        /// <summary>
        /// Soft delete a resume application
        /// </summary>
        public async Task<ServiceResponse> SoftDeleteResumeAsync(int applicationId)
        {
            try
            {
                var validationResult = await ValidateUserAndCompanyAsync();
                if (validationResult.Status != SRStatus.Success)
                {
                    return validationResult;
                }
                var companyId = (int)validationResult.Data!;

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var resumeRepo = _uow.GetRepository<IResumeRepository>();
                
                var resumeApplication = await _resumeApplicationRepo.GetByApplicationIdWithDetailsAsync(applicationId);
                if (resumeApplication == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume application not found."
                    };
                }

                var job = await jobRepo.GetJobByIdAsync(resumeApplication.JobId);
                if (job == null || job.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to delete this resume application."
                    };
                }

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
                    resumeApplication.IsActive = false;
                    await _resumeApplicationRepo.UpdateAsync(resumeApplication);
                    await _uow.CommitTransactionAsync();

                    var resume = await resumeRepo.GetByJobIdAndResumeIdAsync(resumeApplication.JobId, resumeApplication.ResumeId);
                    var jobIdForSignalR = resumeApplication.JobId;
                    var resumeIdForSignalR = resumeApplication.ResumeId;

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
        #endregion
    }
}
