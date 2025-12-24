using System;
using System.Linq;
using System.Threading.Tasks;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using ServiceResponse = Data.Models.Response.ServiceResponse;
using SRStatus = Data.Enum.SRStatus;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using BusinessObjectLayer.Common;

namespace BusinessObjectLayer.Services
{
    public class CandidateService : ICandidateService
    {
        private readonly IUnitOfWork _uow;
        private readonly IResumeApplicationRepository _resumeApplicationRepo;
        private readonly IResumeRepository _resumeRepo;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CandidateService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();
            _resumeRepo = _uow.GetRepository<IResumeRepository>();
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task<(ServiceResponse? errorResponse, int? companyId)> GetCurrentUserCompanyIdAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "User not authenticated."
                }, null);
            }

            int userId = int.Parse(userIdClaim);

            var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
            var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
            if (companyUser == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Company user not found."
                }, null);
            }

            if (companyUser.CompanyId == null)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "You are not associated with any company."
                }, null);
            }

            if (companyUser.JoinStatus != JoinStatusEnum.Approved)
            {
                return (new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = "You must be approved to access company data."
                }, null);
            }

            return (null, companyUser.CompanyId.Value);
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Page and pageSize must be greater than zero."
                    };
                }

                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var repo = _uow.GetRepository<ICandidateRepository>();
                var candidates = await repo.GetPagedByCompanyIdAsync(companyId, page, pageSize, search);
                var total = await repo.GetTotalByCompanyIdAsync(companyId, search);

                var data = candidates.Select(c => new CandidateResponse
                {
                    CandidateId = c.CandidateId,
                    FullName = c.FullName,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    CreatedAt = c.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidates retrieved successfully.",
                    Data = new PaginatedCandidateResponse
                    {
                        Candidates = data,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalCount = total
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get candidates error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving candidates."
                };
            }
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var repo = _uow.GetRepository<ICandidateRepository>();
                var candidate = await repo.GetByIdAsync(id);

                if (candidate == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Candidate not found."
                    };
                }

                // Check if candidate belongs to this company (has resume in company OR application for company job)
                var hasAccess = await repo.HasResumeOrApplicationInCompanyAsync(id, companyId);

                if (!hasAccess)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Candidate not found."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidate retrieved successfully.",
                    Data = new CandidateResponse
                    {
                        CandidateId = candidate.CandidateId,
                        FullName = candidate.FullName,
                        Email = candidate.Email,
                        PhoneNumber = candidate.PhoneNumber,
                        CreatedAt = candidate.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get candidate error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the candidate."
                };
            }
        }

        public async Task<ServiceResponse> GetByIdWithResumesAsync(int id)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var candidateRepo = _uow.GetRepository<ICandidateRepository>();
                var candidate = await candidateRepo.GetByIdAsync(id);

                if (candidate == null || !candidate.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Candidate not found."
                    };
                }

                // Check if candidate has resume or application for this company
                var hasAccess = await candidateRepo.HasResumeOrApplicationInCompanyAsync(id, companyId);

                if (!hasAccess)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Candidate not found."
                    };
                }

                // Get only resumes for this company
                var resumes = await _resumeRepo.GetByCandidateIdAndCompanyIdAsync(id, companyId);

                var candidateResponse = new CandidateResponse
                {
                    CandidateId = candidate.CandidateId,
                    FullName = candidate.FullName,
                    Email = candidate.Email,
                    PhoneNumber = candidate.PhoneNumber,
                    CreatedAt = candidate.CreatedAt
                };

                var resumesResponse = new List<CandidateResumeResponse>();
                foreach (var resume in resumes)
                {
                    var latestApplication = await _resumeApplicationRepo.GetByResumeIdAsync(resume.ResumeId);
                    resumesResponse.Add(new CandidateResumeResponse
                    {
                        ResumeId = resume.ResumeId,
                        CompanyId = resume.CompanyId,
                        FileUrl = resume.FileUrl,
                        FileName = resume.OriginalFileName,
                        Status = resume.Status,
                        IsLatest = resume.IsLatest,
                        CreatedAt = resume.CreatedAt
                    });
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidate with resumes retrieved successfully.",
                    Data = new CandidateWithResumesResponse
                    {
                        Candidate = candidateResponse,
                        Resumes = resumesResponse
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get candidate with resumes error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the candidate with resumes."
                };
            }
        }

        public async Task<ServiceResponse> CreateAsync(CandidateCreateRequest request)
        {
            try
            {
                var repo = _uow.GetRepository<ICandidateRepository>();

                var candidate = new Candidate
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber
                };

                await repo.CreateAsync(candidate);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidate created successfully.",
                    Data = new CandidateResponse
                    {
                        CandidateId = candidate.CandidateId,
                        FullName = candidate.FullName,
                        Email = candidate.Email,
                    PhoneNumber = candidate.PhoneNumber,
                        CreatedAt = candidate.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create candidate error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the candidate."
                };
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, CandidateUpdateRequest request)
        {
            try
            {
                if (request.FullName == null &&
                    request.Email == null &&
                    request.PhoneNumber == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one field must be provided for update."
                    };
                }

                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var repo = _uow.GetRepository<ICandidateRepository>();
                var candidate = await repo.GetByIdAsync(id);

                if (candidate == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Candidate not found."
                    };
                }

                // Check if candidate belongs to this company (has resume in company OR application for company job)
                var hasAccess = await repo.HasResumeOrApplicationInCompanyAsync(id, companyId);

                if (!hasAccess)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to update this candidate."
                    };
                }

                if (request.FullName != null) candidate.FullName = request.FullName;
                if (request.Email != null) candidate.Email = request.Email;
                if (request.PhoneNumber != null) candidate.PhoneNumber = request.PhoneNumber;

                await repo.UpdateAsync(candidate);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidate updated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update candidate error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the candidate."
                };
            }
        }

        public async Task<ServiceResponse> DeleteAsync(int id)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var repo = _uow.GetRepository<ICandidateRepository>();
                var candidate = await repo.GetByIdAsync(id);

                if (candidate == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Candidate not found."
                    };
                }

                // Check if candidate belongs to this company (has resume in company OR application for company job)
                var hasAccess = await repo.HasResumeOrApplicationInCompanyAsync(id, companyId);

                if (!hasAccess)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to delete this candidate."
                    };
                }

                await repo.SoftDeleteAsync(candidate);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Candidate deleted successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete candidate error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting the candidate."
                };
            }
        }

        public async Task<ServiceResponse> GetResumeApplicationsAsync(int resumeId, GetResumeApplicationsRequest request)
        {
            try
            {
                if (request.Page <= 0 || request.PageSize <= 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Page and pageSize must be greater than zero."
                    };
                }

                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var resume = await _resumeRepo.GetByIdAsync(resumeId);
                if (resume == null || !resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // Check if resume belongs to this company
                if (resume.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Resume not found."
                    };
                }

                var (applications, totalCount) = await _resumeApplicationRepo.GetByResumeIdWithJobAndCompanyPagedAsync(
                    resumeId, companyId, request.Page, request.PageSize,
                    request.Search, request.MinScore, request.MaxScore, request.ApplicationStatus, request.SortBy, request.ProcessingMode);

                var applicationList = applications.Select(a => new CandidateApplicationResponse
                    {
                        ApplicationId = a.ApplicationId,
                        ResumeId = a.ResumeId,
                        CandidateId = a.CandidateId,
                        JobId = a.JobId,
                        JobTitle = a.Job?.Title ?? string.Empty,
                        CompanyId = a.Job?.CompanyId ?? 0,
                        CompanyName = a.Job?.Company?.Name ?? string.Empty,
                        CampaignId = a.CampaignId,
                        CampaignTitle = a.Campaign?.Title,
                        ResumeStatus = a.Resume?.Status,
                        ApplicationStatus = a.Status,
                        ApplicationErrorType = a.ErrorType,
                        ProcessingMode = a.ProcessingMode,
                        TotalScore = a.TotalScore,
                        AdjustedScore = a.AdjustedScore,
                        MatchSkills = a.MatchSkills,
                        MissingSkills = a.MissingSkills,
                        Note = a.Note,
                        CreatedAt = a.CreatedAt
                    }).ToList();

                var paginatedResponse = new PaginatedCandidateApplicationResponse
                {
                    Applications = applicationList,
                    TotalCount = totalCount,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Applications retrieved successfully.",
                    Data = paginatedResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get resume applications error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving applications."
                };
            }
        }

        public async Task<ServiceResponse> GetResumeApplicationDetailAsync(int resumeId, int applicationId)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var resume = await _resumeRepo.GetByIdAsync(resumeId);
                if (resume == null || !resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                // Check if resume belongs to this company
                if (resume.CompanyId != companyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Resume not found."
                    };
                }

                var application = await _resumeApplicationRepo.GetByResumeAndApplicationIdWithDetailsAndCompanyAsync(resumeId, applicationId, companyId);
                if (application == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Application not found."
                    };
                }

                var detail = new CandidateApplicationDetailResponse
                {
                    ApplicationId = application.ApplicationId,
                    ResumeId = application.ResumeId,
                    CandidateId = application.CandidateId,
                    JobId = application.JobId,
                    JobTitle = application.Job?.Title ?? string.Empty,
                    CompanyId = application.Job?.CompanyId ?? 0,
                    CompanyName = application.Job?.Company?.Name ?? string.Empty,
                    CampaignId = application.CampaignId,
                    CampaignTitle = application.Campaign?.Title,
                    ResumeStatus = application.Resume?.Status,
                    ApplicationStatus = application.Status,
                    ApplicationErrorType = application.ErrorType,
                    ProcessingMode = application.ProcessingMode,
                    TotalScore = application.TotalScore,
                    AdjustedScore = application.AdjustedScore,
                    CreatedAt = application.CreatedAt,
                    MatchSkills = application.MatchSkills,
                    MissingSkills = application.MissingSkills,
                    Note = application.Note,
                    RequiredSkills = application.RequiredSkills,
                    AIExplanation = application.AIExplanation,
                    ErrorMessage = application.ErrorMessage,
                    ScoreDetails = application.ScoreDetails.Select(sd => new ResumeScoreDetailResponse
                    {
                        CriteriaId = sd.CriteriaId,
                        CriteriaName = sd.Criteria?.Name ?? string.Empty,
                        Matched = sd.Matched,
                        Score = sd.Score,
                        AINote = sd.AINote
                    }).ToList()
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Application retrieved successfully.",
                    Data = detail
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get resume application detail error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the application."
                };
            }
        }

        public async Task<ServiceResponse> GetResumesByCandidateAsync(int candidateId)
        {
            try
            {
                // Get company ID from current user
                var companyIdResult = await GetCurrentUserCompanyIdAsync();
                if (companyIdResult.errorResponse != null)
                {
                    return companyIdResult.errorResponse;
                }
                int companyId = companyIdResult.companyId!.Value;

                var candidateRepo = _uow.GetRepository<ICandidateRepository>();
                var candidate = await candidateRepo.GetByIdAsync(candidateId);
                if (candidate == null || !candidate.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Candidate not found."
                    };
                }

                // Check if candidate has resume or application for this company
                var hasAccess = await candidateRepo.HasResumeOrApplicationInCompanyAsync(candidateId, companyId);

                if (!hasAccess)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Candidate's resumes not found."
                    };
                }

                // Get only resumes for this company
                var resumes = await _resumeRepo.GetByCandidateIdAndCompanyIdAsync(candidateId, companyId);

                var response = new List<CandidateResumeResponse>();
                foreach (var resume in resumes)
                {
                    var latestApplication = await _resumeApplicationRepo.GetByResumeIdAsync(resume.ResumeId);
                    response.Add(new CandidateResumeResponse
                    {
                        ResumeId = resume.ResumeId,
                        CompanyId = resume.CompanyId,
                        FileUrl = resume.FileUrl,
                        FileName = resume.OriginalFileName,
                        Status = resume.Status,
                        IsLatest = resume.IsLatest,
                        CreatedAt = resume.CreatedAt
                    });
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Resumes retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get resumes by candidate error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving resumes."
                };
            }
        }
    }
}


