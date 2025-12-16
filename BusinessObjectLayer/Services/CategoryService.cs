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
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _uow;

        public CategoryService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var categoryRepo = _uow.GetRepository<ICategoryRepository>();
            var categories = await categoryRepo.GetCategoriesAsync(page, pageSize, search);
            var total = await categoryRepo.GetTotalCategoriesAsync(search);

            var pagedData = categories.Select(c => new CategoryResponse
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                CreatedAt = c.CreatedAt ?? DateTime.UtcNow
            }).ToList();

            var responseData = new
            {
                Categories = pagedData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Categories retrieved successfully.",
                Data = responseData
            };
        }


        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var categoryRepo = _uow.GetRepository<ICategoryRepository>();
            var category = await categoryRepo.GetByIdAsync(id);
            if (category == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Data = new CategoryResponse
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name,
                    CreatedAt = category.CreatedAt ?? DateTime.UtcNow
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(CategoryRequest request)
        {
            var categoryRepo = _uow.GetRepository<ICategoryRepository>();
            
            if (await categoryRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var category = new Category
                {
                    Name = request.Name
                };

                await categoryRepo.AddAsync(category);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Category created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, CategoryRequest request)
        {
            var categoryRepo = _uow.GetRepository<ICategoryRepository>();
            var category = await categoryRepo.GetForUpdateAsync(id);
            if (category == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrEmpty(request.Name))
                    category.Name = request.Name;

                categoryRepo.Update(category);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Category updated successfully."
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
            var categoryRepo = _uow.GetRepository<ICategoryRepository>();
            var category = await categoryRepo.GetForUpdateAsync(id);
            if (category == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                category.IsActive = false;
                categoryRepo.Update(category);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Category deactivated successfully."
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
