using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
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

        public JobService(IJobRepository jobRepository, IAuthRepository authRepository)
        {
            _jobRepository = jobRepository;
            _authRepository = authRepository;
        }

        public async Task<ServiceResponse> CreateJobAsync(JobRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                // Get user email from claims
                var emailClaim = userClaims.FindFirst(ClaimTypes.Email)?.Value
                     ?? userClaims.FindFirst("email")?.Value;

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
                var companyUser = await _jobRepository.GetCompanyUserByUserIdAsync(user.UserId);
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
                        Status = SRStatus.Error,
                        Message = "You must join a company before creating a job."
                    };
                }

                // Validate criteria weights sum to 1.0 if criteria are provided
                // if (request.Criteria != null && request.Criteria.Any())
                // {
                //     var totalWeight = request.Criteria.Sum(c => c.Weight);
                //     if (Math.Abs(totalWeight - 1.0m) > 0.001m) // Allow small floating point errors
                //     {
                //         return new ServiceResponse
                //         {
                //             Status = SRStatus.Error,
                //             Message = $"Criteria weights must sum to 1.0. Current sum: {totalWeight}"
                //         };
                //     }
                // }

                // Create job entity
                var job = new Job
                {
                    ComUserId = companyUser.ComUserId,
                    CompanyId = companyUser.CompanyId.Value,  // Safe because we checked above
                    Title = request.Title ?? string.Empty,
                    Description = request.Description,
                    Slug = GenerateSlug(request.Title ?? string.Empty),
                    Requirements = request.Requirements,
                    JobStatus = JobStatusEnum.Pending,
                    IsActive = true
                };

                // Save job
                var createdJob = await _jobRepository.CreateJobAsync(job);

                // Add job categories if provided
                if (request.CategoryIds != null && request.CategoryIds.Any())
                {
                    var jobCategories = request.CategoryIds.Select(categoryId => new JobCategory
                    {
                        JobId = createdJob.JobId,
                        CategoryId = categoryId
                    }).ToList();

                    await _jobRepository.AddJobCategoriesAsync(jobCategories);
                }

                // Add job employment types if provided
                if (request.EmploymentTypeIds != null && request.EmploymentTypeIds.Any())
                {
                    var jobEmploymentTypes = request.EmploymentTypeIds.Select(employTypeId => new JobEmploymentType
                    {
                        JobId = createdJob.JobId,
                        EmployTypeId = employTypeId
                    }).ToList();

                    await _jobRepository.AddJobEmploymentTypesAsync(jobEmploymentTypes);
                }

                // Add criteria if provided
                // if (request.Criteria != null && request.Criteria.Any())
                // {
                //     var criteria = request.Criteria.Select(c => new Criteria
                //     {
                //         JobId = createdJob.JobId,
                //         Name = c.Name,
                //         Weight = c.Weight,
                //         IsActive = true
                //     }).ToList();

                //     await _jobRepository.AddCriteriaAsync(criteria);
                // }

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

                // Map to response
                var jobResponse = MapToJobResponse(jobWithRelations);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job created successfully.",
                    Data = jobResponse
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

                var jobResponse = MapToJobResponse(job);

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

        private JobResponse MapToJobResponse(Job job)
        {
            return new JobResponse
            {
                JobId = job.JobId,
                ComUserId = job.ComUserId,
                CompanyId = job.CompanyId,
                CompanyName = job.Company?.Name,
                Title = job.Title,
                Description = job.Description,
                Slug = job.Slug,
                Requirements = job.Requirements,
            };
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


