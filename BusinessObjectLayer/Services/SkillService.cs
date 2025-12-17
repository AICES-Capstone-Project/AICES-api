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
    public class SkillService : ISkillService
    {
        private readonly IUnitOfWork _uow;

        public SkillService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var (skills, total) = await skillRepo.GetPagedAsync(page, pageSize, search);

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
                Data = new Data.Models.Response.Pagination.PaginatedSkillResponse
                {
                    Skills = result,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total
                }
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var skill = await skillRepo.GetByIdAsync(id);
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
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            
            if (await skillRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Skill name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var skill = new Skill
                {
                    Name = request.Name
                };

                await skillRepo.AddAsync(skill);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Skill created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, SkillRequest request)
        {
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var skill = await skillRepo.GetForUpdateAsync(id);
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
                skill.Name = request.Name ?? skill.Name;
                skillRepo.Update(skill);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Skill updated successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var skillRepo = _uow.GetRepository<ISkillRepository>();
            var skill = await skillRepo.GetForUpdateAsync(id);
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
                skill.IsActive = false;
                skillRepo.Update(skill);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Skill deleted successfully."
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
