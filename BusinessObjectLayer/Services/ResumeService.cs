using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
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
        private readonly IGoogleCloudStorageService _storageService;
        private readonly RedisHelper _redisHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResumeService(
            IParsedResumeRepository parsedResumeRepository,
            IParsedCandidateRepository parsedCandidateRepository,
            IAIScoreRepository aiScoreRepository,
            IAIScoreDetailRepository aiScoreDetailRepository,
            IJobRepository jobRepository,
            ICompanyUserRepository companyUserRepository,
            IGoogleCloudStorageService storageService,
            RedisHelper redisHelper,
            IHttpContextAccessor httpContextAccessor)
        {
            _parsedResumeRepository = parsedResumeRepository;
            _parsedCandidateRepository = parsedCandidateRepository;
            _aiScoreRepository = aiScoreRepository;
            _aiScoreDetailRepository = aiScoreDetailRepository;
            _jobRepository = jobRepository;
            _companyUserRepository = companyUserRepository;
            _storageService = storageService;
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

                // Upload file to Google Cloud Storage
                var uploadResult = await _storageService.UploadResumeAsync(file, "resumes");
                if (uploadResult.Status != SRStatus.Success)
                {
                    return uploadResult;
                }

                // Extract signed URL from upload result
                string? fileUrl = null;
                if (uploadResult.Data != null)
                {
                    // Use reflection or JSON deserialization to extract URL
                    var dataType = uploadResult.Data.GetType();
                    var urlProperty = dataType.GetProperty("Url");
                    if (urlProperty != null)
                    {
                        fileUrl = urlProperty.GetValue(uploadResult.Data) as string;
                    }
                }

                if (string.IsNullOrEmpty(fileUrl))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to get signed URL from upload."
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
                    ResumeStatus = ResumeStatusEnum.Pending,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createdResume = await _parsedResumeRepository.CreateAsync(parsedResume);

                // Push job to Redis queue
                var jobData = new
                {
                    resumeId = createdResume.ResumeId,
                    queueJobId = queueJobId,
                    jobId = jobId,
                    fileUrl = fileUrl
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
                Console.WriteLine($"Error uploading resume: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while uploading the resume."
                };
            }
        }

        public async Task<ServiceResponse> ProcessAIResultAsync(AIResultRequest request)
        {
            try
            {
                // Find ParsedResume by queueJobId
                var parsedResume = await _parsedResumeRepository.GetByQueueJobIdAsync(request.QueueJobId);
                if (parsedResume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found for the given queue job ID."
                    };
                }

                // Verify resumeId matches
                if (parsedResume.ResumeId != request.ResumeId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Resume ID mismatch."
                    };
                }

                // Update ParsedResume status and data
                parsedResume.ResumeStatus = ResumeStatusEnum.Completed;
                parsedResume.Data = request.RawJson != null ? JsonSerializer.Serialize(request.RawJson) : null;
                await _parsedResumeRepository.UpdateAsync(parsedResume);

                // Create AIScores record
                var aiScore = new AIScores
                {
                    TotalResumeScore = request.TotalResumeScore,
                    AIExplanation = request.AIExplanation,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createdScore = await _aiScoreRepository.CreateAsync(aiScore);

                // Create ParsedCandidate record (if not exists)
                var existingCandidate = await _parsedCandidateRepository.GetByResumeIdAsync(parsedResume.ResumeId);
                if (existingCandidate == null)
                {
                    // Note: In a real scenario, you might extract candidate info from the parsed data
                    // For now, we'll create a placeholder
                    var parsedCandidate = new ParsedCandidates
                    {
                        ResumeId = parsedResume.ResumeId,
                        JobId = parsedResume.JobId,
                        ScoreId = createdScore.ScoreId,
                        FullName = "Unknown", // Should be extracted from parsed data
                        Email = "unknown@example.com", // Should be extracted from parsed data
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _parsedCandidateRepository.CreateAsync(parsedCandidate);
                }
                else
                {
                    // Update existing candidate with score ID
                    existingCandidate.ScoreId = createdScore.ScoreId;
                    await _parsedCandidateRepository.UpdateAsync(existingCandidate);
                }

                // Create AIScoreDetail records
                var scoreDetails = request.AIScoreDetail.Select(detail => new AIScoreDetail
                {
                    CriteriaId = detail.CriteriaId,
                    ScoreId = createdScore.ScoreId,
                    Matched = detail.Matched,
                    Score = detail.Score,
                    AINote = detail.AINote,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _aiScoreDetailRepository.CreateRangeAsync(scoreDetails);

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
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while processing the AI result."
                };
            }
        }

        public async Task<ServiceResponse> GetResumeResultAsync(int resumeId)
        {
            try
            {
                var parsedResume = await _parsedResumeRepository.GetByIdWithDetailsAsync(resumeId);
                if (parsedResume == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // If status is Pending, return only status
                if (parsedResume.ResumeStatus == ResumeStatusEnum.Pending)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume is still being processed.",
                        Data = new ResumeResultResponse
                        {
                            Status = ResumeStatusEnum.Pending
                        }
                    };
                }

                // If status is Completed, return full data
                if (parsedResume.ResumeStatus == ResumeStatusEnum.Completed)
                {
                    var candidate = parsedResume.ParsedCandidates;
                    if (candidate == null || candidate.AIScores == null)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.NotFound,
                            Message = "AI score data not found for this resume."
                        };
                    }

                    var aiScore = candidate.AIScores;
                    var scoreDetails = aiScore.AIScoreDetails?.Select(detail => new AIScoreDetailResponse
                    {
                        CriteriaId = detail.CriteriaId,
                        CriteriaName = detail.Criteria?.Name ?? "",
                        Matched = detail.Matched,
                        Score = detail.Score,
                        AINote = detail.AINote
                    }).ToList() ?? new List<AIScoreDetailResponse>();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Resume result retrieved successfully.",
                        Data = new ResumeResultResponse
                        {
                            Status = ResumeStatusEnum.Completed,
                            Data = new ResumeResultData
                            {
                                ResumeId = parsedResume.ResumeId,
                                TotalResumeScore = aiScore.TotalResumeScore,
                                AIExplanation = aiScore.AIExplanation,
                                AIScoreDetails = scoreDetails
                            }
                        }
                    };
                }

                // For other statuses (Failed, Cancelled)
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resume processing status retrieved.",
                    Data = new ResumeResultResponse
                    {
                        Status = parsedResume.ResumeStatus
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting resume result: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the resume result."
                };
            }
        }
    }
}

