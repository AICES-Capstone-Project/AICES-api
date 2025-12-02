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
                js.JobSkillId,
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

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkill = await jobSkillRepo.GetByIdAsync(id);
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
                jobSkill.JobSkillId,
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
            var job = await jobRepo.GetByIdAsync(request.JobId);

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

        public async Task<ServiceResponse> UpdateAsync(int id, JobSkillRequest request)
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkill = await jobSkillRepo.GetByIdAsync(id);
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
                jobSkill.JobId = request.JobId;
                jobSkill.SkillId = request.SkillId;

                jobSkillRepo.Update(jobSkill);
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

        public async Task<ServiceResponse> DeleteAsync(int id)
        {
            var jobSkillRepo = _uow.GetRepository<IJobSkillRepository>();
            var jobSkill = await jobSkillRepo.GetByIdForUpdateAsync(id);
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
