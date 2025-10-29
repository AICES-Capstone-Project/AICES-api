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
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var categories = await _categoryRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(search))
            {
                categories = categories
                    .Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var total = categories.Count();

            var pagedData = categories
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryResponse
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt ?? DateTime.UtcNow
                })
                .ToList();

            var responseData = new
            {
                Categories = pagedData,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
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
            var category = await _categoryRepository.GetByIdAsync(id);
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
                    IsActive = category.IsActive,
                    CreatedAt = category.CreatedAt ?? DateTime.UtcNow
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(CategoryRequest request)
        {
            if (await _categoryRepository.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category name already exists."
                };
            }

            var category = new Category
            {
                Name = request.Name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _categoryRepository.AddAsync(category);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Category created successfully."
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, CategoryRequest request)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category not found."
                };
            }

            
            if (!string.IsNullOrEmpty(request.Name))
                category.Name = request.Name;

            await _categoryRepository.UpdateAsync(category);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Category updated successfully."
            };
        }


        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Category not found."
                };
            }

            category.IsActive = false;
            await _categoryRepository.UpdateAsync(category);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Category deactivated successfully."
            };
        }
    }
}
