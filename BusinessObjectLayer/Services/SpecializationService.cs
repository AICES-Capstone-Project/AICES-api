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
    public class SpecializationService : ISpecializationService
    {
        private readonly ISpecializationRepository _specializationRepository;
        private readonly ICategoryRepository _categoryRepository;

        public SpecializationService(
            ISpecializationRepository specializationRepository,
            ICategoryRepository categoryRepository)
        {
            _specializationRepository = specializationRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var paged = await _specializationRepository.GetPagedAsync(page, pageSize, search);
                var total = await _specializationRepository.GetTotalCountAsync(search);

                var pagedData = paged
                    .Select(s => new SpecializationResponse
                    {
                        SpecializationId = s.SpecializationId,
                        Name = s.Name,
                        CategoryId = s.CategoryId,
                        CategoryName = s.Category?.Name,
                        IsActive = s.IsActive,
                        CreatedAt = s.CreatedAt ?? DateTime.UtcNow
                    })
                    .ToList();

                var responseData = new
                {
                    Specializations = pagedData,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specializations retrieved successfully.",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get specializations error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving specializations."
                };
            }
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            try
            {
                var specialization = await _specializationRepository.GetByIdAsync(id);
                if (specialization == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Specialization not found."
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specialization retrieved successfully.",
                    Data = new SpecializationResponse
                    {
                        SpecializationId = specialization.SpecializationId,
                        Name = specialization.Name,
                        CategoryId = specialization.CategoryId,
                        CategoryName = specialization.Category?.Name,
                        IsActive = specialization.IsActive,
                        CreatedAt = specialization.CreatedAt ?? DateTime.UtcNow
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get specialization error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving the specialization."
                };
            }
        }

        public async Task<ServiceResponse> CreateAsync(SpecializationRequest request)
        {
            try
            {
                // Validate category exists
                var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId);
                if (!categoryExists)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Category with ID {request.CategoryId} does not exist."
                    };
                }

                // Check if specialization name already exists
                if (await _specializationRepository.ExistsByNameAsync(request.Name))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "Specialization name already exists."
                    };
                }

                var specialization = new Specialization
                {
                    Name = request.Name,
                    CategoryId = request.CategoryId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _specializationRepository.AddAsync(specialization);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specialization created successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create specialization error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating the specialization."
                };
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, SpecializationRequest request)
        {
            try
            {
                var specialization = await _specializationRepository.GetByIdAsync(id);
                if (specialization == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Specialization not found."
                    };
                }

                // Validate category if it's being changed
                if (request.CategoryId != specialization.CategoryId)
                {
                    var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId);
                    if (!categoryExists)
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Validation,
                            Message = $"Category with ID {request.CategoryId} does not exist."
                        };
                    }
                }

                // Check if name already exists (excluding current specialization)
                if (!string.IsNullOrEmpty(request.Name) && request.Name != specialization.Name)
                {
                    if (await _specializationRepository.ExistsByNameAsync(request.Name))
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Duplicated,
                            Message = "Specialization name already exists."
                        };
                    }
                }

                // Update fields
                if (!string.IsNullOrEmpty(request.Name))
                    specialization.Name = request.Name;

                specialization.CategoryId = request.CategoryId;

                await _specializationRepository.UpdateAsync(specialization);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specialization updated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update specialization error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating the specialization."
                };
            }
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            try
            {
                var specialization = await _specializationRepository.GetByIdAsync(id);
                if (specialization == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Specialization not found."
                    };
                }

                specialization.IsActive = false;
                await _specializationRepository.UpdateAsync(specialization);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specialization deactivated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Soft delete specialization error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deactivating the specialization."
                };
            }
        }
    }
}

