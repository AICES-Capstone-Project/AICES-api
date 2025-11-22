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
    public class SkillService : ISkillService
    {
        private readonly ISkillRepository _skillRepository;

        public SkillService(ISkillRepository skillRepository)
        {
            _skillRepository = skillRepository;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var skills = await _skillRepository.GetAllAsync();
            var result = skills.Select(s => new SkillResponse
            {
                SkillId = s.SkillId,
                Name = s.Name,
                CreatedAt = s.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Skills retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var skill = await _skillRepository.GetByIdAsync(id);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Skill retrieved successfully.",
                Data = new SkillResponse
                {
                    SkillId = skill.SkillId,
                    Name = skill.Name,
                    CreatedAt = skill.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(SkillRequest request)
        {
            if (await _skillRepository.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Skill name already exists."
                };
            }

            var skill = new Skill
            {
                Name = request.Name
            };

            await _skillRepository.AddAsync(skill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Skill created successfully."
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, SkillRequest request)
        {
            var skill = await _skillRepository.GetByIdAsync(id);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            skill.Name = request.Name ?? skill.Name;

            await _skillRepository.UpdateAsync(skill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Skill updated successfully."
            };
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var skill = await _skillRepository.GetByIdAsync(id);
            if (skill == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Skill not found."
                };
            }

            await _skillRepository.SoftDeleteAsync(skill);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Skill deleted successfully."
            };
        }
    }
}
