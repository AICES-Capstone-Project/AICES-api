using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class JobService : IJobService
    {
        private readonly IJobRepository _jobRepository;
        private readonly IAuthRepository _authRepository;
        private readonly ICompanyUserRepository _companyUserRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IEmploymentTypeRepository _employmentTypeRepository;
        private readonly IJobCategoryRepository _jobCategoryRepository;
        private readonly IJobEmploymentTypeRepository _jobEmploymentTypeRepository;
        private readonly ICriteriaService _criteriaService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JobService(
            IJobRepository jobRepository, 
            IAuthRepository authRepository,
            ICompanyUserRepository companyUserRepository,
            ICategoryRepository categoryRepository,
            IEmploymentTypeRepository employmentTypeRepository,
            IJobCategoryRepository jobCategoryRepository,
            IJobEmploymentTypeRepository jobEmploymentTypeRepository,
            ICriteriaService criteriaService,
            IHttpContextAccessor httpContextAccessor)
        {
            _jobRepository = jobRepository;
            _authRepository = authRepository;
            _companyUserRepository = companyUserRepository;
            _categoryRepository = categoryRepository;
            _employmentTypeRepository = employmentTypeRepository;
            _jobCategoryRepository = jobCategoryRepository;
            _jobEmploymentTypeRepository = jobEmploymentTypeRepository;
            _criteriaService = criteriaService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse> GetJobByIdAsync(int jobId)
        {
            try
            {
                var job = await _jobRepository.GetJobByIdAsync(jobId);

                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job retrieved successfully.",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get job error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the job."
                };
            }
        }

        public async Task<ServiceResponse> GetJobsAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var jobs = await _jobRepository.GetJobsAsync(page, pageSize, search);
                var total = await _jobRepository.GetTotalJobsAsync(search);

                var jobResponses = jobs.Select(j => new JobResponse
                {
                    JobId = j.JobId,
                    ComUserId = j.ComUserId,
                    CompanyId = j.CompanyId,
                    CompanyName = j.Company?.Name ?? "",
                    Title = j.Title,
                    Description = j.Description,
                    Slug = j.Slug,
                    Requirements = j.Requirements,
                    JobStatus = j.JobStatus,
                    IsActive = j.IsActive,
                    CreatedAt = j.CreatedAt ?? DateTime.MinValue,
                    Categories = j.JobCategories?.Select(jc => jc.Category?.Name ?? "").ToList() ?? new List<string>(),
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>()
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Jobs retrieved successfully.",
                    Data = new PaginatedJobResponse
                    {
                        Jobs = jobResponses,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get jobs error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving jobs."
                };
            }
        }

        public async Task<ServiceResponse> SelfCompanyCreateJobAsync(JobRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                // Get user email from claims
             var emailClaim = Common.ClaimUtils.GetEmailClaim(userClaims);

                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Email claim not found in token."
                    };
                }

                // Get user from database
                var user = await _authRepository.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not found."
                    };
                }

                // Get CompanyUser for this user
                var companyUser = await _companyUserRepository.GetCompanyUserByUserIdAsync(user.UserId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "User is not associated with any company."
                    };
                }

                //Check if user has joined a company
                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You must join a company before creating a job."
                    };
                }

                // Determine JobStatus based on user role
                var jobStatus = user.Role?.RoleName == "HR_Manager" 
                    ? JobStatusEnum.Published 
                    : JobStatusEnum.Pending;

                // Create job entity
                var job = new Job
                {
                    ComUserId = companyUser.ComUserId,
                    CompanyId = companyUser.CompanyId.Value,  // Safe because we checked above
                    Title = request.Title ?? string.Empty,
                    Description = request.Description,
                    Slug = GenerateSlug(request.Title ?? string.Empty),
                    Requirements = request.Requirements,
                    JobStatus = jobStatus,
                    IsActive = true
                };

                // Save job
                var createdJob = await _jobRepository.CreateJobAsync(job);

                // Validate and add job categories
                if (request.CategoryIds == null || request.CategoryIds.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one category is required."
                    };
                }

                // Validate that all category IDs exist
                foreach (var categoryId in request.CategoryIds)
                {
                    var categoryExists = await _categoryRepository.ExistsAsync(categoryId);
                    if (!categoryExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = $"Category with ID {categoryId} does not exist."
                        };
                    }
                }

                var jobCategories = request.CategoryIds.Select(categoryId => new JobCategory
                {
                    JobId = createdJob.JobId,
                    CategoryId = categoryId
                }).ToList();

                await _jobCategoryRepository.AddJobCategoriesAsync(jobCategories);

                // Validate and add job employment types
                if (request.EmploymentTypeIds == null || request.EmploymentTypeIds.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one employment type is required."
                    };
                }

                // Validate that all employment type IDs exist
                foreach (var employTypeId in request.EmploymentTypeIds)
                {
                    var employmentTypeExists = await _employmentTypeRepository.ExistsAsync(employTypeId);
                    if (!employmentTypeExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = $"Employment type with ID {employTypeId} does not exist."
                        };
                    }
                }

                var jobEmploymentTypes = request.EmploymentTypeIds.Select(employTypeId => new JobEmploymentType
                {
                    JobId = createdJob.JobId,
                    EmployTypeId = employTypeId
                }).ToList();

                await _jobEmploymentTypeRepository.AddJobEmploymentTypesAsync(jobEmploymentTypes);

                // Validate and create criteria via service
                if (request.Criteria == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Criteria are required."
                    };
                }

                var criteriaResponse = await _criteriaService.CreateCriteriaForJobAsync(createdJob.JobId, request.Criteria);
                if (criteriaResponse.Status != SRStatus.Success)
                {
                    return criteriaResponse;
                }

                // Get the complete job with all relationships
                var jobWithRelations = await _jobRepository.GetJobByIdAsync(createdJob.JobId);

                if (jobWithRelations == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Job created but could not retrieve details."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job created successfully.",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create job error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the job."
                };
            }
        }

        // Get all jobs for the authenticated user's company
        public async Task<ServiceResponse> GetSelfCompanyJobsAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get company user to find associated company
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You are not associated with any company."
                    };
                }

                var jobs = await _jobRepository.GetJobsByCompanyIdAsync(companyUser.CompanyId.Value, page, pageSize, search);
                var total = await _jobRepository.GetTotalJobsByCompanyIdAsync(companyUser.CompanyId.Value, search);

                var jobResponses = jobs.Select(j => new SelfJobResponse
                {
                    JobId = j.JobId,
                    Title = j.Title,
                    Description = j.Description,
                    Slug = j.Slug,
                    Requirements = j.Requirements,
                    Categories = j.JobCategories?.Select(jc => jc.Category?.Name ?? "").ToList() ?? new List<string>(),
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>()
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Jobs retrieved successfully.",
                    Data = new PaginatedSelfJobResponse
                    {
                        Jobs = jobResponses,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        CurrentPage = page,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get self company jobs error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving jobs."
                };
            }
        }

        // Get specific job by ID for the authenticated user's company
        public async Task<ServiceResponse> GetSelfCompanyJobByIdAsync(int jobId)
        {
            try
            {
                // Get current user ID from claims
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? Common.ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get company user to find associated company
                var companyUser = await _companyUserRepository.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You are not associated with any company."
                    };
                }

                var job = await _jobRepository.GetJobByIdAndCompanyIdAsync(jobId, companyUser.CompanyId.Value);

                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found or does not belong to your company."
                    };
                }

                var jobResponse = new SelfJobResponse
                {
                    JobId = job.JobId,
                    Title = job.Title,
                    Description = job.Description,
                    Slug = job.Slug,
                    Requirements = job.Requirements,
                    Categories = job.JobCategories?.Select(jc => jc.Category?.Name ?? "").ToList() ?? new List<string>(),
                    EmploymentTypes = job.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Criteria = job.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>()
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job retrieved successfully.",
                    Data = jobResponse
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get self company job error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the job."
                };
            }
        }

        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Convert to lowercase and replace spaces with hyphens
            var slug = title.ToLowerInvariant().Trim();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            
            return slug;
        }
    }
}


