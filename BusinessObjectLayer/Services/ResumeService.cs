using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.Json;

namespace BusinessObjectLayer.Services
{
    public class ResumeService : IResumeService
    {
        private readonly IParsedResumeRepository _parsedResumeRepository;
        private readonly IParsedCandidateRepository _parsedCandidateRepository;
        private readonly IAIScoreRepository _aiScoreRepository;
        private readonly IAIScoreDetailRepository _aiScoreDetailRepository;
        private readonly IJobRepository _jobRepository;
        private readonly ICompanyUserRepository _companyUserRepository;
        private readonly GoogleCloudStorageHelper _storageHelper;
        private readonly RedisHelper _redisHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResumeService(
            IParsedResumeRepository parsedResumeRepository,
            IParsedCandidateRepository parsedCandidateRepository,
            IAIScoreRepository aiScoreRepository,
            IAIScoreDetailRepository aiScoreDetailRepository,
            IJobRepository jobRepository,
            ICompanyUserRepository companyUserRepository,
            GoogleCloudStorageHelper storageHelper,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor)
        {
            _parsedResumeRepository = parsedResumeRepository;
            _parsedCandidateRepository = parsedCandidateRepository;
            _aiScoreRepository = aiScoreRepository;
            _aiScoreDetailRepository = aiScoreDetailRepository;
            _jobRepository = jobRepository;
            _companyUserRepository = companyUserRepository;
            _storageHelper = storageHelper;
            _redisHelper = redisHelper;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse> UploadResumeAsync(int jobId, IFormFile file)
        {
            try
            {
                // Validate job exists
                var job = await _jobRepository.GetJobByIdAsync(jobId);
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
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }
                int companyId = companyUser.CompanyId.Value;

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

                // Create ParsedResume record
                var parsedResume = new ParsedResumes
                {
                    CompanyId = companyUser.CompanyId.Value,
                    JobId = jobId,
                    QueueJobId = queueJobId,
                    FileUrl = fileUrl,
                    ResumeStatus = ResumeStatusEnum.Pending
                };

                var createdResume = await _parsedResumeRepository.CreateAsync(parsedResume);

                // Prepare criteria data for queue
                var criteriaData = job.Criteria?.Select(c => new CriteriaQueueResponse
                {
                    criteriaId = c.CriteriaId,
                    name = c.Name,
                    weight = c.Weight
                }).ToList() ?? new List<CriteriaQueueResponse>();

                // Push job to Redis queue with requirements and criteria
                var jobData = new ResumeQueueJobResponse
                {
                    resumeId = createdResume.ResumeId,
                    queueJobId = queueJobId,
                    jobId = jobId,
                    fileUrl = fileUrl,
                    requirements = job.Requirements,
                    criteria = criteriaData
                };

                var pushed = await _redisHelper.PushJobAsync("resume_parse_queue", jobData);
                if (!pushed)
                {
                    // Log warning but don't fail the request
                    Console.WriteLine($"Warning: Failed to push job to Redis queue for resumeId: {createdResume.ResumeId}");
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume uploaded successfully.",
                    Data = new ResumeUploadResponse
                    {
                        ResumeId = createdResume.ResumeId,
                        QueueJobId = queueJobId,
                        Status = ResumeStatusEnum.Pending
                    }
                };
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

        public async Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request)
        {
            try
            {
                // 1. Find resume
                var parsedResume = await _parsedResumeRepository.GetByQueueJobIdAsync(request.QueueJobId);
                if (parsedResume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found for the given queue job ID."
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

                await _parsedResumeRepository.UpdateAsync(parsedResume);

                // 4. Save AI Score
                string? aiExplanationString = request.AIExplanation is string s
                    ? s
                    : JsonSerializer.Serialize(request.AIExplanation);

                var aiScore = new AIScores
                {
                    TotalResumeScore = request.TotalResumeScore,
                    AIExplanation = aiExplanationString
                };

                var createdScore = await _aiScoreRepository.CreateAsync(aiScore);

                // 5. Create/Update ParsedCandidate
                var existingCandidate = await _parsedCandidateRepository.GetByResumeIdAsync(parsedResume.ResumeId);

                var fullName = request.CandidateInfo?.FullName ?? "Unknown";
                var email = request.CandidateInfo?.Email ?? "unknown@example.com";
                var phone = request.CandidateInfo?.PhoneNumber;

                if (existingCandidate == null)
                {
                    var parsedCandidate = new ParsedCandidates
                    {
                        ResumeId = parsedResume.ResumeId,
                        JobId = parsedResume.JobId,
                        ScoreId = createdScore.ScoreId,
                        FullName = fullName,
                        Email = email,
                        PhoneNumber = phone
                    };

                    await _parsedCandidateRepository.CreateAsync(parsedCandidate);
                }
                else
                {
                    existingCandidate.ScoreId = createdScore.ScoreId;
                    existingCandidate.FullName = fullName;
                    existingCandidate.Email = email;
                    existingCandidate.PhoneNumber = phone;

                    await _parsedCandidateRepository.UpdateAsync(existingCandidate);
                }

                // 6. Save AIScoreDetail
                var scoreDetails = request.AIScoreDetail.Select(detail => new AIScoreDetail
                {
                    CriteriaId = detail.CriteriaId,
                    ScoreId = createdScore.ScoreId,
                    Matched = detail.Matched,
                    Score = detail.Score,
                    AINote = detail.AINote
                }).ToList();

                await _aiScoreDetailRepository.CreateRangeAsync(scoreDetails);

                // 7. Response
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "AI result saved successfully.",
                    Data = new { resumeId = parsedResume.ResumeId }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing AI result: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                try
                {
                    var parsedResume = await _parsedResumeRepository.GetByQueueJobIdAsync(request.QueueJobId);
                    if (parsedResume != null)
                    {
                        parsedResume.ResumeStatus = ResumeStatusEnum.Failed;
                        await _parsedResumeRepository.UpdateAsync(parsedResume);
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
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await _jobRepository.GetJobByIdAsync(jobId);
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
                var resumes = await _parsedResumeRepository.GetByJobIdAsync(jobId);

                var resumeList = resumes.Select(resume => new JobResumeListResponse
                {
                    ResumeId = resume.ResumeId,
                    Status = resume.ResumeStatus,
                    FullName = resume.ParsedCandidates?.FullName ?? "Unknown",
                    TotalResumeScore = resume.ParsedCandidates?.AIScores?.TotalResumeScore
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
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                
                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await _jobRepository.GetJobByIdAsync(jobId);
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
                var resume = await _parsedResumeRepository.GetByJobIdAndResumeIdAsync(jobId, resumeId);
                if (resume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                var candidate = resume.ParsedCandidates;
                var aiScore = candidate?.AIScores;

                var scoreDetails = aiScore?.AIScoreDetails?.Select(detail => new ResumeScoreDetailResponse
                {
                    CriteriaId = detail.CriteriaId,
                    CriteriaName = detail.Criteria?.Name ?? "",
                    Matched = detail.Matched,
                    Score = detail.Score,
                    AINote = detail.AINote
                }).ToList() ?? new List<ResumeScoreDetailResponse>();

                var response = new JobResumeDetailResponse
                {
                    ResumeId = resume.ResumeId,
                    QueueJobId = resume.QueueJobId ?? string.Empty,
                    FileUrl = resume.FileUrl ?? string.Empty,
                    Status = resume.ResumeStatus,
                    CreatedAt = resume.CreatedAt,
                    FullName = candidate?.FullName ?? "Unknown",
                    Email = candidate?.Email ?? "N/A",
                    PhoneNumber = candidate?.PhoneNumber,
                    TotalResumeScore = aiScore?.TotalResumeScore,
                    AIExplanation = aiScore?.AIExplanation,
                    ScoreDetails = scoreDetails
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
    }
}

