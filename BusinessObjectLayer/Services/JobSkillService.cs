using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class JobSkillService : IJobSkillService
    {
        private readonly IJobSkillRepository _jobSkillRepository;
        private readonly IJobRepository _jobRepository;
        private readonly ISkillRepository _skillRepository;

        public JobSkillService(
            IJobSkillRepository jobSkillRepository,
            IJobRepository jobRepository,
            ISkillRepository skillRepository)
        {
            _jobSkillRepository = jobSkillRepository;
            _jobRepository = jobRepository;
            _skillRepository = skillRepository;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var jobSkills = await _jobSkillRepository.GetAllAsync();

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
            var jobSkill = await _jobSkillRepository.GetByIdAsync(id);
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
            // Kiểm tra tồn tại Job
            var job = await _jobRepository.GetJobByIdAsync(request.JobId);

            if (job == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Job not found."
                };
            }

            // Kiểm tra tồn tại Skill
            var skill = await _skillRepository.GetByIdAsync(request.SkillId);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            var jobSkill = new JobSkill
            {
                JobId = request.JobId,
                SkillId = request.SkillId
            };

            await _jobSkillRepository.AddAsync(jobSkill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "JobSkill created successfully."
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, JobSkillRequest request)
        {
            var jobSkill = await _jobSkillRepository.GetByIdAsync(id);
            if (jobSkill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "JobSkill not found."
                };
            }

            jobSkill.JobId = request.JobId;
            jobSkill.SkillId = request.SkillId;

            await _jobSkillRepository.UpdateAsync(jobSkill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "JobSkill updated successfully."
            };
        }

        public async Task<ServiceResponse> DeleteAsync(int id)
        {
            var jobSkill = await _jobSkillRepository.GetByIdAsync(id);
            if (jobSkill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "JobSkill not found."
                };
            }

            await _jobSkillRepository.DeleteAsync(jobSkill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "JobSkill deleted successfully."
            };
        }
    }
}
