using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class LevelService : ILevelService
    {
        private readonly IUnitOfWork _uow;

        public LevelService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var levelRepo = _uow.GetRepository<ILevelRepository>();
            var levels = await levelRepo.GetAllAsync(page, pageSize, search);
            var total = await levelRepo.GetTotalAsync(search);
            var result = levels.Select(l => new LevelResponse
            {
                LevelId = l.LevelId,
                Name = l.Name,
                CreatedAt = l.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Levels retrieved successfully.",
                Data = new PaginatedLevelResponse
                {
                    Levels = result,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total
                }
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var levelRepo = _uow.GetRepository<ILevelRepository>();
            var level = await levelRepo.GetByIdAsync(id);
            if (level == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Level not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Level retrieved successfully.",
                Data = new LevelResponse
                {
                    LevelId = level.LevelId,
                    Name = level.Name,
                    CreatedAt = level.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(LevelRequest request)
        {
            var levelRepo = _uow.GetRepository<ILevelRepository>();
            
            if (await levelRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Level name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var level = new Level
                {
                    Name = request.Name
                };

                await levelRepo.AddAsync(level);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Level created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, LevelRequest request)
        {
            var levelRepo = _uow.GetRepository<ILevelRepository>();
            var level = await levelRepo.GetForUpdateAsync(id);
            if (level == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Level not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                level.Name = request.Name ?? level.Name;
                levelRepo.Update(level);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Level updated successfully."
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
            var levelRepo = _uow.GetRepository<ILevelRepository>();
            var level = await levelRepo.GetForUpdateAsync(id);
            if (level == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Level not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                level.IsActive = false;
                levelRepo.Update(level);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Level deleted successfully."
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
