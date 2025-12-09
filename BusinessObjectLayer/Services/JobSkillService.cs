using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class JobSkillService : IJobSkillService
    {
        private readonly IUnitOfWork _uow;

        public JobSkillService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkills = await jobSkillRepo.GetAllAsync();

            var result = jobSkills.Select(js => new
            {
                js.JobId,
                JobName = js.Job?.Title,
                js.SkillId,
                SkillName = js.Skill?.Name
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Job skills retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> GetByJobIdAndSkillIdAsync(int jobId, int skillId)
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkill = await jobSkillRepo.GetByJobIdAndSkillIdAsync(jobId, skillId);
            if (jobSkill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "JobSkill not found."
                };
            }

            var result = new
            {
                jobSkill.JobId,
                JobName = jobSkill.Job?.Title,
                jobSkill.SkillId,
                SkillName = jobSkill.Skill?.Name
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "JobSkill retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> CreateAsync(JobSkillRequest request)
        {
            var jobRepo = _uow.GetRepository<IJobRepository>();
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            
            // Kiểm tra tồn tại Job
            var job = await jobRepo.GetJobByIdAsync(request.JobId);

            if (job == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Job not found."
                };
            }

            // Kiểm tra tồn tại Skill
            var skill = await skillRepo.GetByIdAsync(request.SkillId);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var jobSkill = new JobSkill
                {
                    JobId = request.JobId,
                    SkillId = request.SkillId
                };

                await jobSkillRepo.AddAsync(jobSkill);
                await _uow.SaveChangesAsync();
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "JobSkill created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int jobId, int skillId, JobSkillRequest request)
        {
            var jobRepo = _uow.GetRepository<IJobRepository>();
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            
            // Check if JobSkill exists
            var existingJobSkill = await jobSkillRepo.GetForUpdateByJobIdAndSkillIdAsync(jobId, skillId);
            if (existingJobSkill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "JobSkill not found."
                };
            }

            // Validate new Job exists
            var job = await jobRepo.GetJobByIdAsync(request.JobId);
            if (job == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Job not found."
                };
            }

            // Validate new Skill exists
            var skill = await skillRepo.GetByIdAsync(request.SkillId);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                // Delete old relationship
                jobSkillRepo.Delete(existingJobSkill);
                await _uow.SaveChangesAsync();

                // Create new relationship
                var newJobSkill = new JobSkill
                {
                    JobId = request.JobId,
                    SkillId = request.SkillId
                };
                await jobSkillRepo.AddAsync(newJobSkill);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "JobSkill updated successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> DeleteAsync(int jobId, int skillId)
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkill = await jobSkillRepo.GetForUpdateByJobIdAndSkillIdAsync(jobId, skillId);
            if (jobSkill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "JobSkill not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                jobSkillRepo.Delete(jobSkill);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "JobSkill deleted successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
