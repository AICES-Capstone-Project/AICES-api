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

namespace BusinessObjectLayer.Services
{
    public class CandidateService : ICandidateService
    {
        private readonly IUnitOfWork _uow;
        private readonly IResumeApplicationRepository _resumeApplicationRepo;
        private readonly IResumeRepository _resumeRepo;

        public CandidateService(IUnitOfWork uow)
        {
            _uow = uow;
            _resumeApplicationRepo = _uow.GetRepository<IResumeApplicationRepository>();
            _resumeRepo = _uow.GetRepository<IResumeRepository>();
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

                var repo = _uow.GetRepository<ICandidateRepository>();
                var candidates = await repo.GetPagedAsync(page, pageSize, search);
                var total = await repo.GetTotalAsync(search);

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
                        PageSize = pageSize
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

        public async Task<ServiceResponse> GetResumeApplicationsAsync(int resumeId)
        {
            try
            {
                var resume = await _resumeRepo.GetByIdAsync(resumeId);
                if (resume == null || !resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                var applications = await _resumeApplicationRepo.GetByResumeIdWithJobAsync(resumeId);

                var response = applications.Select(a => new CandidateApplicationResponse
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
                    Status = a.Status,
                    TotalScore = a.TotalScore,
                    AdjustedScore = a.AdjustedScore,
                    MatchSkills = a.MatchSkills,
                    MissingSkills = a.MissingSkills,
                    CreatedAt = a.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Applications retrieved successfully.",
                    Data = response
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
                var resume = await _resumeRepo.GetByIdAsync(resumeId);
                if (resume == null || !resume.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Resume not found."
                    };
                }

                var application = await _resumeApplicationRepo.GetByResumeAndApplicationIdWithDetailsAsync(resumeId, applicationId);
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
                    Status = application.Status,
                    TotalScore = application.TotalScore,
                    AdjustedScore = application.AdjustedScore,
                    CreatedAt = application.CreatedAt,
                    MatchSkills = application.MatchSkills,
                    MissingSkills = application.MissingSkills,
                    RequiredSkills = application.RequiredSkills,
                    AIExplanation = application.AIExplanation,
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

                var resumes = await _resumeRepo.GetByCandidateIdAsync(candidateId);
                var response = resumes.Select(r => new CandidateResumeResponse
                {
                    ResumeId = r.ResumeId,
                    CompanyId = r.CompanyId,
                    FileUrl = r.FileUrl,
                    QueueJobId = r.QueueJobId,
                    Status = r.Status,
                    IsLatest = r.IsLatest,
                    CreatedAt = r.CreatedAt
                }).ToList();

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


