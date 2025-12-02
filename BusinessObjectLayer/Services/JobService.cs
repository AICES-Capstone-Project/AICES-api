using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
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
        private readonly IUnitOfWork _uow;
        private readonly ICriteriaService _criteriaService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationService _notificationService;


        public JobService(
            IUnitOfWork uow,
            ICriteriaService criteriaService,
            IHttpContextAccessor httpContextAccessor,
            INotificationService notificationService)
        {
            _uow = uow;
            _criteriaService = criteriaService;
            _httpContextAccessor = httpContextAccessor;
            _notificationService = notificationService;
        }

        public async Task<ServiceResponse> GetJobByIdAsync(int jobId, int companyId)
        {
            try
            {
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var job = await jobRepo.GetByIdAndCompanyIdAsync(jobId, companyId);

                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                var jobResponse = new JobResponse
                {
                    JobId = job.JobId,
                    ComUserId = job.ComUserId,
                    CompanyId = job.CompanyId,
                    CompanyName = job.Company?.Name ?? "",
                    Title = job.Title,
                    Description = job.Description,
                    Slug = job.Slug,
                    Requirements = job.Requirements,
                    JobStatus = job.JobStatus,
                    CreatedAt = job.CreatedAt,
                    CategoryName = job.Specialization?.Category?.Name,
                    SpecializationName = job.Specialization?.Name,
                    EmploymentTypes = job.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = job.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = job.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = job.CompanyUser?.User?.Profile?.FullName
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
                Console.WriteLine($"Get job error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the job."
                };
            }
        }

        public async Task<ServiceResponse> GetJobsAsync(int companyId, int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var jobs = await jobRepo.GetAllJobsByCompanyIdAsync(companyId, page, pageSize, search);
                var total = await jobRepo.CountByCompanyIdAsync(companyId, search);

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
                    CreatedAt = j.CreatedAt,
                    CategoryName = j.Specialization?.Category?.Name,
                    SpecializationName = j.Specialization?.Name,
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = j.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = j.CompanyUser?.User?.Profile?.FullName
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

        public async Task<ServiceResponse> CreateAsync(JobRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                // ============================================
                // PHASE 1: VALIDATE ALL INPUT BEFORE TRANSACTION
                // ============================================
                
                // Get repositories
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                var skillRepo = _uow.GetRepository<ISkillRepository>();
                
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
                var user = await authRepo.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not found."
                    };
                }

                // Get CompanyUser for this user
                var companyUser = await companyUserRepo.GetByUserIdAsync(user.UserId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "User is not associated with any company."
                    };
                }

                // Check if user has joined a company
                if (companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "You must join a company before creating a job."
                    };
                }

                // Check for duplicate job title in company
                if (!string.IsNullOrEmpty(request.Title))
                {
                    var titleExists = await jobRepo.ExistsByTitleAndCompanyIdAsync(request.Title, companyUser.CompanyId.Value);
                    if (titleExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "A job with this title already exists in your company."
                        };
                    }
                }

                // Validate specialization
                if (request.SpecializationId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Specialization is required."
                    };
                }

                var specExists = await specializationRepo.ExistsAsync(request.SpecializationId.Value);
                if (!specExists)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Specialization with ID {request.SpecializationId} does not exist."
                    };
                }

                // Validate employment types
                if (request.EmploymentTypeIds == null || request.EmploymentTypeIds.Count == 0)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least one employment type is required."
                    };
                }

                foreach (var employTypeId in request.EmploymentTypeIds)
                {
                    var employmentTypeExists = await employmentTypeRepo.ExistsAsync(employTypeId);
                    if (!employmentTypeExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = $"Employment type with ID {employTypeId} does not exist."
                        };
                    }
                }

                // Validate skills if provided
                if (request.SkillIds != null && request.SkillIds.Count > 0)
                {
                    foreach (var skillId in request.SkillIds)
                    {
                        var skill = await skillRepo.GetByIdAsync(skillId);
                        if (skill == null)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Skill with ID {skillId} does not exist."
                            };
                        }
                    }
                }

                // Validate criteria
                if (request.Criteria == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Criteria are required."
                    };
                }

                // Validate criteria structure (without creating them yet)
                if (request.Criteria.Count < 2)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "At least 2 criteria are required."
                    };
                }

                if (request.Criteria.Count >= 20)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Maximum of 19 criteria can be provided."
                    };
                }

                var totalWeight = request.Criteria.Sum(c => c.Weight);
                if (Math.Abs(totalWeight - 1.0m) > 0.001m)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Total weight of all criteria must equal 1.0. Current total: {totalWeight}"
                    };
                }

                // ============================================
                // PHASE 2: BEGIN TRANSACTION AND CREATE ENTITIES
                // ============================================
                
                await _uow.BeginTransactionAsync();

                try
                {
                    // Determine JobStatus based on user role
                    var jobStatus = user.Role?.RoleName == "HR_Manager"
                        ? JobStatusEnum.Published
                        : JobStatusEnum.Pending;

                    // Create job entity
                    var job = new Job
                    {
                        ComUserId = companyUser.ComUserId,
                        CompanyId = companyUser.CompanyId.Value,
                        Title = request.Title ?? string.Empty,
                        Description = request.Description,
                        Slug = GenerateSlug(request.Title ?? string.Empty),
                        Requirements = request.Requirements,
                        JobStatus = jobStatus,
                        SpecializationId = request.SpecializationId
                    };

                    // Add job to context
                    await jobRepo.AddAsync(job);
                    
                    // ✅ CRITICAL: Save immediately to generate JobId for FK relationships
                    await _uow.SaveChangesAsync();

                    // Add job employment types (now JobId exists in database)
                    var jobEmploymentTypeRepo = _uow.GetRepository<IJobEmploymentTypeRepository>();
                    var jobEmploymentTypes = request.EmploymentTypeIds.Select(employTypeId => new JobEmploymentType
                    {
                        JobId = job.JobId,
                        EmployTypeId = employTypeId
                    }).ToList();
                    await jobEmploymentTypeRepo.AddRangeAsync(jobEmploymentTypes);

                    // Add job skills if provided
                    if (request.SkillIds != null && request.SkillIds.Count > 0)
                    {
                        var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
                        var jobSkills = request.SkillIds.Select(id => new JobSkill
                        {
                            JobId = job.JobId,
                            SkillId = id
                        }).ToList();
                        await jobSkillRepo.AddRangeAsync(jobSkills);
                    }

                    // Add criteria (JobId now exists, no FK constraint error)
                    var criteriaRepo = _uow.GetRepository<ICriteriaRepository>();
                    var criteria = request.Criteria.Select(c => new Criteria
                    {
                        JobId = job.JobId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList();
                    await criteriaRepo.AddRangeAsync(criteria);

                    // Commit transaction (saves remaining changes and commits)
                    await _uow.CommitTransactionAsync();

                    // ============================================
                    // PHASE 3: POST-COMMIT OPERATIONS (Notifications)
                    // ============================================
                    
                    // try
                    // {
                    //     var admins = await authRepo.GetUsersByRoleAsync("System_Admin");
                    //     foreach (var admin in admins)
                    //     {
                    //         await _notificationService.CreateAsync(
                    //             admin.UserId,
                    //             NotificationTypeEnum.JobCreated,
                    //             $"A new job has been created: {job.Title}"
                    //         );
                    //     }
                    //     Console.WriteLine($"Sent notification for new job: {job.Title}");
                    // }
                    // catch (Exception ex)
                    // {
                    //     Console.WriteLine($"Error sending notification: {ex.Message}");
                    //     // Don't fail the whole operation if notification fails
                    // }

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Job created successfully."
                    };
                }
                catch (Exception)
                {
                    // Rollback transaction on any error
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
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
        public async Task<ServiceResponse> GetCurrentCompanyPublishedListAsync(int page = 1, int pageSize = 10, string? search = null)
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access company jobs."
                    };
                }

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var jobs = await jobRepo.GetPublishedJobsByCompanyIdAsync(companyUser.CompanyId.Value, page, pageSize, search);
                var total = await jobRepo.CountPublishedByCompanyIdAsync(companyUser.CompanyId.Value, search);

                var jobResponses = jobs.Select(j => new SelfJobResponse
                {
                    JobId = j.JobId,
                    Title = j.Title,
                    Description = j.Description,
                    Slug = j.Slug,
                    Requirements = j.Requirements,
                    JobStatus = j.JobStatus,
                    CreatedAt = j.CreatedAt,
                    CategoryName = j.Specialization?.Category?.Name,
                    SpecializationName = j.Specialization?.Name,
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = j.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = j.CompanyUser?.User?.Profile?.FullName
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

        // Get all jobs (all statuses) for HR_Manager to manage
        public async Task<ServiceResponse> GetCurrentCompanyPendingListAsync(int page = 1, int pageSize = 10, string? search = null)
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var jobs = await jobRepo.GetPendingJobsByCompanyIdAsync(companyUser.CompanyId.Value, page, pageSize, search);
                var total = await jobRepo.CountPendingByCompanyIdAsync(companyUser.CompanyId.Value, search);

                var jobResponses = jobs.Select(j => new ManagerJobResponse
                {
                    JobId = j.JobId,
                    Title = j.Title,
                    Description = j.Description,
                    Slug = j.Slug,
                    Requirements = j.Requirements,
                    JobStatus = j.JobStatus,
                    CreatedAt = j.CreatedAt,
                    CategoryName = j.Specialization?.Category?.Name,
                    SpecializationName = j.Specialization?.Name,
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = j.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = j.CompanyUser?.User?.Profile?.FullName
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Jobs retrieved successfully.",
                    Data = new PaginatedManagerJobResponse
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
        public async Task<ServiceResponse> GetCurrentCompanyPublishedByIdAsync(int jobId)
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
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access company jobs."
                    };
                }

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var job = await jobRepo.GetPublishedJobByIdAndCompanyIdAsync(jobId, companyUser.CompanyId.Value);

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
                    CategoryName = job.Specialization?.Category?.Name,
                    SpecializationName = job.Specialization?.Name,
                    JobStatus = job.JobStatus,
                    EmploymentTypes = job.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = job.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = job.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = job.CompanyUser?.User?.Profile?.FullName
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

        public async Task<ServiceResponse> GetCurrentCompanyPendingByIdAsync(int jobId)
        {
            try
            {
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

                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
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

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var job = await jobRepo.GetPendingJobByIdAndCompanyIdAsync(jobId, companyUser.CompanyId.Value);
                if (job == null || job.JobStatus != JobStatusEnum.Pending)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Pending job not found or does not belong to your company."
                    };
                }

                var jobResponse = new ManagerJobResponse
                {
                    JobId = job.JobId,
                    Title = job.Title,
                    Description = job.Description,
                    Slug = job.Slug,
                    Requirements = job.Requirements,
                    JobStatus = job.JobStatus,
                    CreatedAt = job.CreatedAt,
                    CategoryName = job.Specialization?.Category?.Name,
                    SpecializationName = job.Specialization?.Name,
                    EmploymentTypes = job.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = job.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = job.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
                    FullName = job.CompanyUser?.User?.Profile?.FullName
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
                Console.WriteLine($"Get self company pending job error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the job."
                };
            }
        }

        // Get all jobs created by the current user (me)
        public async Task<ServiceResponse> GetCurrentUserJobsAsync(int page = 1, int pageSize = 10, string? search = null, JobStatusEnum? status = null)
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

                // Get company user to find ComUserId
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);
                if (companyUser == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company user not found."
                    };
                }

                if (companyUser.JoinStatus != JoinStatusEnum.Approved)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You must be approved or invited to access jobs."
                    };
                }

                var jobRepo = _uow.GetRepository<IJobRepository>();
                var jobs = await jobRepo.GetListByCreatorIdAsync(companyUser.ComUserId, page, pageSize, search, status);
                var total = await jobRepo.CountByCreatorIdAsync(companyUser.ComUserId, search, status);

                var jobResponses = jobs.Select(j => new SelfJobResponse
                {
                    JobId = j.JobId,
                    Title = j.Title,
                    Description = j.Description,
                    Slug = j.Slug,
                    Requirements = j.Requirements,
                    JobStatus = j.JobStatus,
                    CreatedAt = j.CreatedAt,
                    CategoryName = j.Specialization?.Category?.Name,
                    SpecializationName = j.Specialization?.Name,
                    EmploymentTypes = j.JobEmploymentTypes?.Select(jet => jet.EmploymentType?.Name ?? "").ToList() ?? new List<string>(),
                    Skills = j.JobSkills?.Select(s => s.Skill.Name).ToList() ?? new List<string>(),
                    Criteria = j.Criteria?.Select(c => new CriteriaResponse
                    {
                        CriteriaId = c.CriteriaId,
                        Name = c.Name,
                        Weight = c.Weight
                    }).ToList() ?? new List<CriteriaResponse>(),
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
                Console.WriteLine($"Get self jobs by me error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving jobs."
                };
            }
        }

        public async Task<ServiceResponse> GetCurrentUserJobsWithStatusStringAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            JobStatusEnum? jobStatus = null;
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<JobStatusEnum>(status, true, out var parsedStatus))
                {
                    jobStatus = parsedStatus;
                }
                else
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Invalid status value: {status}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(JobStatusEnum)))}"
                    };
                }
            }

            return await GetCurrentUserJobsAsync(page, pageSize, search, jobStatus);
        }

        // Update a job for the authenticated user's company (basic fields and specialization)
        public async Task<ServiceResponse> UpdateAsync(int jobId, JobRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                // ============================================
                // PHASE 1: VALIDATE INPUT
                // ============================================
                
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                var skillRepo = _uow.GetRepository<ISkillRepository>();
                var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
                
                var emailClaim = Common.ClaimUtils.GetEmailClaim(userClaims);
                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "Email claim not found in token." };
                }

                var user = await authRepo.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not found." };
                }

                var companyUser = await companyUserRepo.GetByUserIdAsync(user.UserId);
                if (companyUser?.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "You must join a company before updating a job." };
                }

                var job = await jobRepo.GetPublishedByIdAndCompanyIdForUpdateAsync(jobId, companyUser.CompanyId.Value);
                if (job == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Job not found or does not belong to your company." };
                }

                // Validate specialization if provided
                if (request.SpecializationId != null)
                {
                    var specExists = await specializationRepo.ExistsAsync(request.SpecializationId.Value);
                    if (!specExists)
                    {
                        return new ServiceResponse { Status = SRStatus.Validation, Message = $"Specialization with ID {request.SpecializationId} does not exist." };
                    }
                }

                // Validate skills if provided
                if (request.SkillIds != null)
                {
                    foreach (var skillId in request.SkillIds)
                    {
                        var skill = await skillRepo.GetByIdAsync(skillId);
                        if (skill == null)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Skill with ID {skillId} does not exist."
                            };
                        }
                    }
                }

                // Validate employment types if provided
                if (request.EmploymentTypeIds != null)
                {
                    foreach (var etId in request.EmploymentTypeIds)
                    {
                        var exist = await employmentTypeRepo.ExistsAsync(etId);
                        if (!exist)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Employment type with ID {etId} does not exist."
                            };
                        }
                    }
                }

                // Validate criteria if provided
                if (request.Criteria != null)
                {
                    if (request.Criteria.Count < 2)
                    {
                        return new ServiceResponse { Status = SRStatus.Validation, Message = "At least 2 criteria are required." };
                    }

                    if (request.Criteria.Count >= 20)
                    {
                        return new ServiceResponse { Status = SRStatus.Validation, Message = "Maximum of 19 criteria can be provided." };
                    }

                    var totalWeight = request.Criteria.Sum(c => c.Weight);
                    if (Math.Abs(totalWeight - 1.0m) > 0.001m)
                    {
                        return new ServiceResponse { Status = SRStatus.Validation, Message = $"Total weight of all criteria must equal 1.0. Current total: {totalWeight}" };
                    }
                }

                // ============================================
                // PHASE 2: BEGIN TRANSACTION AND UPDATE
                // ============================================
                
                await _uow.BeginTransactionAsync();

                try
                {
                    // Update basic job fields
                    if (!string.IsNullOrEmpty(request.Title))
                    {
                        job.Title = request.Title;
                        job.Slug = GenerateSlug(request.Title);
                    }
                    if (request.Description != null) job.Description = request.Description;
                    if (request.Requirements != null) job.Requirements = request.Requirements;
                    if (request.SpecializationId != null) job.SpecializationId = request.SpecializationId;

                    await jobRepo.UpdateAsync(job);

                    // Replace job skills if provided
                    if (request.SkillIds != null)
                    {
                        var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
                        await jobSkillRepo.DeleteByJobIdAsync(job.JobId);
                        var newJobSkills = request.SkillIds.Select(id => new JobSkill
                        {
                            JobId = job.JobId,
                            SkillId = id
                        }).ToList();
                        await jobSkillRepo.AddRangeAsync(newJobSkills);
                    }

                    // Replace criteria if provided
                    if (request.Criteria != null)
                    {
                        var criteriaRepo = _uow.GetRepository<ICriteriaRepository>();
                        await criteriaRepo.DeleteByJobIdAsync(job.JobId);
                        var criteria = request.Criteria.Select(c => new Criteria
                        {
                            JobId = job.JobId,
                            Name = c.Name,
                            Weight = c.Weight
                        }).ToList();
                        await criteriaRepo.AddRangeAsync(criteria);
                    }

                    // Replace employment types if provided
                    if (request.EmploymentTypeIds != null)
                    {
                        var jobEmploymentTypeRepo = _uow.GetRepository<IJobEmploymentTypeRepository>();
                        await jobEmploymentTypeRepo.DeleteByJobIdAsync(job.JobId);
                        var newJets = request.EmploymentTypeIds.Select(id => new JobEmploymentType
                        {
                            JobId = job.JobId,
                            EmployTypeId = id
                        }).ToList();
                        await jobEmploymentTypeRepo.AddRangeAsync(newJets);
                    }

                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse { Status = SRStatus.Success, Message = "Job updated successfully." };
                }
                catch (Exception)
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update job error: {ex.Message}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while updating the job." };
            }
        }

        // Soft delete a job for the authenticated user's company
        public async Task<ServiceResponse> DeleteAsync(int jobId, ClaimsPrincipal userClaims)
        {
            try
            {
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var parsedResumeRepo = _uow.GetRepository<IParsedResumeRepository>();
                
                var emailClaim = Common.ClaimUtils.GetEmailClaim(userClaims);
                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "Email claim not found in token." };
                }

                var user = await authRepo.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not found." };
                }

                var companyUser = await companyUserRepo.GetByUserIdAsync(user.UserId);
                if (companyUser?.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "You must join a company before deleting a job." };
                }

                var job = await jobRepo.GetPublishedByIdAndCompanyIdForUpdateAsync(jobId, companyUser.CompanyId.Value);
                if (job == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Job not found or does not belong to your company." };
                }

                // Prevent deleting job if there is any resume associated with this job
                var jobResumes = await parsedResumeRepo.GetByJobIdAsync(job.JobId);
                if (jobResumes != null && jobResumes.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Cannot delete this job because there are existing resumes associated with it."
                    };
                }

                await _uow.BeginTransactionAsync();

                try
                {
                    await jobRepo.SoftDeleteAsync(job);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse { Status = SRStatus.Success, Message = "Job deleted successfully." };
                }
                catch (Exception)
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete job error: {ex.Message}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "An error occurred while deleting the job." };
            }
        }

        public async Task<ServiceResponse> UpdateStatusAsync(int jobId, JobStatusEnum status, ClaimsPrincipal userClaims)
        {
            try
            {
                var authRepo = _uow.GetRepository<IAuthRepository>();
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var jobRepo = _uow.GetRepository<IJobRepository>();
                
                var emailClaim = Common.ClaimUtils.GetEmailClaim(userClaims);
                if (string.IsNullOrEmpty(emailClaim))
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "Email claim not found in token." };
                }

                var user = await authRepo.GetByEmailAsync(emailClaim);
                if (user == null)
                {
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not found." };
                }

                var companyUser = await companyUserRepo.GetByUserIdAsync(user.UserId);
                if (companyUser?.CompanyId == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "You must join a company before updating job status." };
                }

                // Use GetByIdAndCompanyIdForUpdateAsync to get job regardless of current status
                var job = await jobRepo.GetByIdAndCompanyIdForUpdateAsync(jobId, companyUser.CompanyId.Value);
                if (job == null)
                {
                    return new ServiceResponse { Status = SRStatus.NotFound, Message = "Job not found or does not belong to your company." };
                }

                // Role-based validation for status transitions
                var userRole = user.Role?.RoleName;

                // Validate transitions based on role
                if (userRole == "HR_Recruiter")
                {
                    // HR_Recruiter can only create jobs (status is set at creation)
                    // They cannot change status after creation
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "HR_Recruiter cannot update job status. Only HR_Manager can manage job status."
                    };
                }

                if (userRole != "HR_Manager")
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "Only HR_Manager can update job status."
                    };
                }

                // Validate status transitions based on current status and new status
                switch (job.JobStatus)
                {
                    case JobStatusEnum.Pending:
                        // From Pending: can only go to Published or Rejected
                        if (status != JobStatusEnum.Published && status != JobStatusEnum.Rejected)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Cannot change status from Pending to {status}. Only Published or Rejected are allowed."
                            };
                        }
                        break;

                    case JobStatusEnum.Published:
                        // From Published: can only go to Archived
                        if (status != JobStatusEnum.Archived)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Cannot change status from Published to {status}. Only Archived is allowed."
                            };
                        }
                        break;

                    case JobStatusEnum.Rejected:
                        // From Rejected: terminal state, cannot change
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = "Cannot change status from Rejected. This is a terminal state."
                        };

                    case JobStatusEnum.Archived:
                        // From Archived: can only go back to Published (un-archive)
                        if (status != JobStatusEnum.Published)
                        {
                            return new ServiceResponse
                            {
                                Status = SRStatus.Validation,
                                Message = $"Cannot change status from Archived to {status}. Only Published (un-archive) is allowed."
                            };
                        }
                        break;
                }

                // Update job status
                await _uow.BeginTransactionAsync();

                try
                {
                    job.JobStatus = status;
                    await jobRepo.UpdateAsync(job);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = $"Job status updated to {status} successfully."
                    };
                }
                catch (Exception)
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update job status error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating job status."
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


