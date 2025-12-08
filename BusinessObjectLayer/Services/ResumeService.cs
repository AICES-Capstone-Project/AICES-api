using BusinessObjectLayer.Common;
using BusinessObjectLayer.Hubs;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Design;
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
        
        // Application-level lock per company to prevent race conditions
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _companyLocks = new();

        public ResumeService(
            IUnitOfWork uow,
            GoogleCloudStorageHelper storageHelper,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor,
            IResumeLimitService resumeLimitService,
            IHubContext<ResumeHub> hubContext)
        {
            _uow = uow;
            _storageHelper = storageHelper;
            _redisHelper = redisHelper;
            _httpContextAccessor = httpContextAccessor;
            _resumeLimitService = resumeLimitService;
            _hubContext = hubContext;
        }

        public async Task<ServiceResponse> UploadResumeAsync(int jobId, IFormFile file)
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
                // Validate job exists
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

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

                // Validate that the job belongs to the user's company
                if (job.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "This jobId not exists in your company."
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

                        // Create ParsedResume record
                        var parsedResume = new ParsedResumes
                        {
                            CompanyId = companyUser.CompanyId.Value,
                            JobId = jobId,
                            QueueJobId = queueJobId,
                            FileUrl = fileUrl,
                            ResumeStatus = ResumeStatusEnum.Pending
                        };

                        await parsedResumeRepo.CreateAsync(parsedResume);
                        await _uow.SaveChangesAsync(); // Get ResumeId

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

                        // Push job to Redis queue 
                        var jobData = new ResumeQueueJobResponse
                        {
                            resumeId = parsedResume.ResumeId,
                            queueJobId = queueJobId,
                            jobId = jobId,
                            fileUrl = fileUrl,
                            requirements = job.Requirements,
                            skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
                            specialization = job.Specialization?.Name,
                            employmentType = employmentTypesList.Any() ? string.Join(", ", employmentTypesList) : null,
                            criteria = criteriaData,
                            mode = "parse"
                        };

                        var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                        if (!pushed)
                        {
                            // Log warning but don't fail the request
                            Console.WriteLine($"Warning: Failed to push job to Redis queue for resumeId: {parsedResume.ResumeId}");
                        }

                        // Store job data with queueJobId as key for tracking and retrieval (expires in 24 hours)
                        // await _redisHelper.SetJobDataAsync($"resume:job:{queueJobId}", jobData, TimeSpan.FromHours(24));
                        // // Also store with resumeId for easy lookup
                        // await _redisHelper.SetJobDataAsync($"resume:data:{parsedResume.ResumeId}", jobData, TimeSpan.FromHours(24));

                        await _uow.CommitTransactionAsync();

                        // Reload resume for SignalR to get fresh data
                        var uploadedResume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(jobId, parsedResume.ResumeId);
                        
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(jobId, parsedResume.ResumeId, "uploaded", null, uploadedResume);
                        });

                        return new ServiceResponse
                        {
                            Status = SRStatus.Success,
                            Message = "Resume uploaded successfully.",
                            Data = new ResumeUploadResponse
                            {
                                ResumeId = parsedResume.ResumeId,
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
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
                var resume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
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
                if (resume.ResumeStatus != ResumeStatusEnum.Completed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Cannot rescore resume with status '{resume.ResumeStatus}'. Only 'Completed' resumes can be rescored."
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

                // Push job to Redis queue with mode = "rescore" and parsedData
                var jobData = new ResumeQueueJobResponse
                {
                    resumeId = resume.ResumeId,
                    queueJobId = newQueueJobId,
                    jobId = jobId,
                    fileUrl = resume.FileUrl ?? string.Empty,
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
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
                    resume.ResumeStatus = ResumeStatusEnum.Pending;
                    await parsedResumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var rescoreResume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(jobId, resume.ResumeId);
                    
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                var parsedCandidateRepo = _uow.GetRepository<IParsedCandidateRepository>();
                var aiScoreRepo = _uow.GetRepository<IAIScoreRepository>();
                var aiScoreDetailRepo = _uow.GetRepository<IAIScoreDetailRepository>();
                
                // 1. Find resume
                var parsedResume = await parsedResumeRepo.GetByQueueJobIdAsync(request.QueueJobId);
                if (parsedResume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found for the given queue job ID."
                    };
                }

                // 1.5 Handle AI-side validation errors (e.g. not a resume)
                if (!string.IsNullOrWhiteSpace(request.Error))
                {
                    await _uow.BeginTransactionAsync();
                    try
                    {
                        parsedResume.ResumeStatus = ResumeStatusEnum.Invalid;
                        parsedResume.Data = JsonSerializer.Serialize(
                            new { error = request.Error },
                            new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                WriteIndented = false
                            });

                        await parsedResumeRepo.UpdateAsync(parsedResume);
                        await _uow.CommitTransactionAsync();

                        // Reload resume for SignalR
                        var resumeForSignalR = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(parsedResume.JobId, parsedResume.ResumeId);
                        
                        // Send real-time SignalR update
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(200);
                            await SendResumeUpdateAsync(parsedResume.JobId, parsedResume.ResumeId, "status_changed", 
                                new { newStatus = "Invalid" }, resumeForSignalR);
                        });
                    }
                    catch
                    {
                        await _uow.RollbackTransactionAsync();
                        throw;
                    }

                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Uploaded file is not a valid resume.",
                        Data = new
                        {
                            resumeId = parsedResume.ResumeId,
                            status = ResumeStatusEnum.Invalid
                        }
                    };
                }

                // 2. Validate resumeId
                if (parsedResume.ResumeId != request.ResumeId)
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

                if (request.AIScoreDetail == null || request.AIScoreDetail.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "AIScoreDetail is required."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // 3. Update parsed resume (JSON + Completed)
                    parsedResume.ResumeStatus = ResumeStatusEnum.Completed;

                    if (request.RawJson != null)
                    {
                        parsedResume.Data = request.RawJson is string rawJsonString
                            ? rawJsonString
                            : JsonSerializer.Serialize(request.RawJson, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                WriteIndented = false
                            });
                    }

                    await parsedResumeRepo.UpdateAsync(parsedResume);
                    await _uow.SaveChangesAsync();

                    // 4. Create/Update ParsedCandidate first
                    var existingCandidate = await parsedCandidateRepo.GetByResumeIdAsync(parsedResume.ResumeId);

                    var fullName = request.CandidateInfo?.FullName ?? "Unknown";
                    var email = request.CandidateInfo?.Email ?? "unknown@example.com";
                    var phone = request.CandidateInfo?.PhoneNumber;
                    var matchSkills = request.CandidateInfo?.MatchSkills;
                    var missingSkills = request.CandidateInfo?.MissingSkills;

                    ParsedCandidates parsedCandidate;
                    if (existingCandidate == null)
                    {
                        parsedCandidate = new ParsedCandidates
                        {
                            ResumeId = parsedResume.ResumeId,
                            JobId = parsedResume.JobId,
                            FullName = fullName,
                            Email = email,
                            PhoneNumber = phone,
                            MatchSkills = matchSkills,
                            MissingSkills = missingSkills
                        };

                        await parsedCandidateRepo.CreateAsync(parsedCandidate);
                        await _uow.SaveChangesAsync(); // Get CandidateId
                    }
                    else
                    {
                        existingCandidate.FullName = fullName;
                        existingCandidate.Email = email;
                        existingCandidate.PhoneNumber = phone;
                        existingCandidate.MatchSkills = matchSkills;
                        existingCandidate.MissingSkills = missingSkills;

                        await parsedCandidateRepo.UpdateAsync(existingCandidate);
                        parsedCandidate = existingCandidate;
                    }

                    // 5. Save AI Score with CandidateId
                    string? aiExplanationString = request.AIExplanation switch
                    {
                        string s => s,
                        System.Text.Json.JsonElement je => je.GetString() ?? je.GetRawText(),
                        null => null,
                        _ => request.AIExplanation.ToString()
                    };

                    var aiScore = new AIScores
                    {
                        CandidateId = parsedCandidate.CandidateId,
                        TotalResumeScore = request.TotalResumeScore.Value,
                        AIExplanation = aiExplanationString
                    };

                    await aiScoreRepo.CreateAsync(aiScore);
                    await _uow.SaveChangesAsync(); // Get ScoreId

                    // 6. Save AIScoreDetail
                    var scoreDetails = request.AIScoreDetail.Select(detail => new AIScoreDetail
                    {
                        CriteriaId = detail.CriteriaId,
                        ScoreId = aiScore.ScoreId,
                        Matched = detail.Matched,
                        Score = detail.Score,
                        AINote = detail.AINote
                    }).ToList();

                    await aiScoreDetailRepo.CreateRangeAsync(scoreDetails);
                    await _uow.SaveChangesAsync();
                    await _uow.CommitTransactionAsync();

                    // Reload resume with all includes to get fresh data for SignalR
                    var resumeForSignalR = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(parsedResume.JobId, parsedResume.ResumeId);
                    
                    // Send real-time SignalR update (fire and forget, but with proper data)
                    _ = Task.Run(async () => 
                    {
                        // Small delay to ensure all changes are persisted
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(parsedResume.JobId, parsedResume.ResumeId, "status_changed", 
                            new { newStatus = "Completed" }, resumeForSignalR);
                    });

                    // 7. Response
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "AI result saved successfully.",
                        Data = new { resumeId = parsedResume.ResumeId }
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
                    var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                    var parsedResume = await parsedResumeRepo.GetByQueueJobIdAsync(request.QueueJobId);
                    if (parsedResume != null)
                    {
                        await _uow.BeginTransactionAsync();
                        try
                        {
                            parsedResume.ResumeStatus = ResumeStatusEnum.Failed;
                            await parsedResumeRepo.UpdateAsync(parsedResume);
                            await _uow.CommitTransactionAsync();

                            // Reload resume for SignalR
                            var resumeForSignalR = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(parsedResume.JobId, parsedResume.ResumeId);
                            
                            // Send real-time SignalR update
                            _ = Task.Run(async () => 
                            {
                                await Task.Delay(200);
                                await SendResumeUpdateAsync(parsedResume.JobId, parsedResume.ResumeId, "status_changed", 
                                    new { newStatus = "Failed" }, resumeForSignalR);
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

        public async Task<ServiceResponse> GetJobResumesAsync(int jobId)
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
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

                // Get all resumes for this job
                var resumes = await parsedResumeRepo.GetByJobIdAsync(jobId);

                var resumeList = resumes.Select(resume => new JobResumeListResponse
                {
                    ResumeId = resume.ResumeId,
                    Status = resume.ResumeStatus,
                    FullName = resume.ParsedCandidates?.FullName ?? "Unknown",
                    TotalResumeScore = resume.ParsedCandidates?.AIScores?.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.TotalResumeScore
                }).ToList();

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

        public async Task<ServiceResponse> GetJobResumeDetailAsync(int jobId, int resumeId)
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
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

                // Get resume with full details
                var resume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                var candidate = resume.ParsedCandidates;
                
                // Get all AIScores for this candidate, ordered by CreatedAt descending (newest first)
                var aiScoresResponse = candidate?.AIScores?
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(aiScore => new AIScoreResponse
                    {
                        ScoreId = aiScore.ScoreId,
                        TotalResumeScore = aiScore.TotalResumeScore,
                        AIExplanation = aiScore.AIExplanation,
                        CreatedAt = aiScore.CreatedAt,
                        ScoreDetails = aiScore.AIScoreDetails?.Select(detail => new ResumeScoreDetailResponse
                        {
                            CriteriaId = detail.CriteriaId,
                            CriteriaName = detail.Criteria?.Name ?? "",
                            Matched = detail.Matched,
                            Score = detail.Score,
                            AINote = detail.AINote
                        }).ToList() ?? new List<ResumeScoreDetailResponse>()
                    }).ToList() ?? new List<AIScoreResponse>();

                var response = new JobResumeDetailResponse
                {
                    ResumeId = resume.ResumeId,
                    QueueJobId = resume.QueueJobId ?? string.Empty,
                    FileUrl = resume.FileUrl ?? string.Empty,
                    Status = resume.ResumeStatus,
                    CreatedAt = resume.CreatedAt,
                    CandidateId = candidate?.CandidateId ?? 0,
                    FullName = candidate?.FullName ?? "Unknown",
                    Email = candidate?.Email ?? "N/A",
                    PhoneNumber = candidate?.PhoneNumber,
                    MatchSkills = candidate?.MatchSkills,
                    MissingSkills = candidate?.MissingSkills,
                    AIScores = aiScoresResponse
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
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
                var resume = await parsedResumeRepo.GetForUpdateAsync(resumeId);
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
                if (resume.ResumeStatus != ResumeStatusEnum.Failed)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Cannot retry resume with status '{resume.ResumeStatus}'. Only 'Failed' resumes can be retried."
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

                // Get job with requirements and criteria
                var job = await jobRepo.GetJobByIdAsync(resume.JobId);
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
                var skillsList = await jobRepo.GetSkillsByJobIdAsync(resume.JobId);
                var employmentTypesList = await jobRepo.GetEmploymentTypesByJobIdAsync(resume.JobId);

                // Push job to Redis queue with requirements and criteria
                var jobData = new ResumeQueueJobResponse
                {
                    resumeId = resume.ResumeId,
                    queueJobId = newQueueJobId,
                    jobId = resume.JobId,
                    fileUrl = resume.FileUrl ?? string.Empty,
                    requirements = job.Requirements,
                    skills = skillsList.Any() ? string.Join(", ", skillsList) : null,
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
                    resume.ResumeStatus = ResumeStatusEnum.Pending;
                    await parsedResumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Reload resume for SignalR
                    var retryResume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(resume.JobId, resume.ResumeId);
                    
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(resume.JobId, resume.ResumeId, "retried", 
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
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
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
                var resume = await parsedResumeRepo.GetForUpdateAsync(resumeId);
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
                    await parsedResumeRepo.UpdateAsync(resume);
                    await _uow.CommitTransactionAsync();

                    // Note: For deleted resumes, we still send update but resume will be null in query
                    // Send real-time SignalR update
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(200);
                        await SendResumeUpdateAsync(resume.JobId, resume.ResumeId, "deleted", null, resume);
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
        private async Task SendResumeUpdateAsync(int jobId, int resumeId, string eventType, object? data = null, ParsedResumes? resume = null)
        {
            try
            {
                // If resume is not provided, query it (with small delay to ensure transaction committed)
                if (resume == null)
                {
                    await Task.Delay(100); // Small delay to ensure transaction is committed
                    var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                    resume = await parsedResumeRepo.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                }
                
                if (resume == null)
                {
                    Console.WriteLine($"⚠️ SignalR: Resume {resumeId} not found, cannot send update");
                    return;
                }

                var updateData = new
                {
                    eventType, // "uploaded", "status_changed", "deleted", "retried"
                    jobId,
                    resumeId,
                    status = resume.ResumeStatus.ToString(),
                    fullName = resume.ParsedCandidates?.FullName ?? "Unknown",
                    totalResumeScore = resume.ParsedCandidates?.AIScores?.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.TotalResumeScore,
                    timestamp = DateTime.UtcNow,
                    data
                };

                // Send to job group (all users watching this job)
                await _hubContext.Clients.Group($"job-{jobId}")
                    .SendAsync("ResumeUpdated", updateData);

                Console.WriteLine($"📡 SignalR: Sent {eventType} update for resume {resumeId} in job {jobId} - Status: {resume.ResumeStatus}, Score: {updateData.totalResumeScore}");
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

