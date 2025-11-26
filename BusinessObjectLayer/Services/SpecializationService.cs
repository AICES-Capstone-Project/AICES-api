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
    public class SpecializationService : ISpecializationService
    {
        private readonly IUnitOfWork _uow;

        public SpecializationService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                var paged = await specializationRepo.GetPagedAsync(page, pageSize, search);
                var total = await specializationRepo.GetTotalCountAsync(search);

                var pagedData = paged
                    .Select(s => new SpecializationResponse
                    {
                        SpecializationId = s.SpecializationId,
                        Name = s.Name,
                        CategoryId = s.CategoryId,
                        CategoryName = s.Category?.Name,
                        CreatedAt = s.CreatedAt,
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
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                var specialization = await specializationRepo.GetByIdAsync(id);
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
                        CreatedAt = specialization.CreatedAt
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

        public async Task<ServiceResponse> GetByCategoryIdAsync(int categoryId)
        {
            try
            {
                var categoryRepo = _uow.GetRepository<ICategoryRepository>();
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                
                // Validate category exists
                var categoryExists = await categoryRepo.ExistsAsync(categoryId);
                if (!categoryExists)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Category not found."
                    };
                }

                var specializations = await specializationRepo.GetByCategoryIdAsync(categoryId);

                var responseData = specializations
                    .Select(s => new SpecializationResponse
                    {
                        SpecializationId = s.SpecializationId,
                        Name = s.Name,
                        CategoryId = s.CategoryId,
                        CategoryName = s.Category?.Name,
                        CreatedAt = s.CreatedAt
                    })
                    .ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Specializations retrieved successfully.",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get specializations by category error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving specializations."
                };
            }
        }

        public async Task<ServiceResponse> CreateAsync(SpecializationRequest request)
        {
            try
            {
                var categoryRepo = _uow.GetRepository<ICategoryRepository>();
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                
                // Validate category exists
                var categoryExists = await categoryRepo.ExistsAsync(request.CategoryId);
                if (!categoryExists)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = $"Category with ID {request.CategoryId} does not exist."
                    };
                }

                // Check if specialization name already exists
                if (await specializationRepo.ExistsByNameAsync(request.Name))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "Specialization name already exists."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    var specialization = new Specialization
                    {
                        Name = request.Name,
                        CategoryId = request.CategoryId
                    };

                    await specializationRepo.AddAsync(specialization);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Specialization created successfully."
                    };
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
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
                var categoryRepo = _uow.GetRepository<ICategoryRepository>();
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                
                var specialization = await specializationRepo.GetByIdAsync(id);
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
                    var categoryExists = await categoryRepo.ExistsAsync(request.CategoryId);
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
                    if (await specializationRepo.ExistsByNameAsync(request.Name))
                    {
                        return new ServiceResponse
                        {
                            Status = SRStatus.Duplicated,
                            Message = "Specialization name already exists."
                        };
                    }
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    // Update fields
                    if (!string.IsNullOrEmpty(request.Name))
                        specialization.Name = request.Name;

                    specialization.CategoryId = request.CategoryId;

                    specializationRepo.Update(specialization);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Specialization updated successfully."
                    };
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
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
                var specializationRepo = _uow.GetRepository<ISpecializationRepository>();
                var specialization = await specializationRepo.GetByIdAsync(id);
                if (specialization == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Specialization not found."
                    };
                }

                await _uow.BeginTransactionAsync();
                try
                {
                    specialization.IsActive = false;
                    specializationRepo.Update(specialization);
                    await _uow.CommitTransactionAsync();

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Specialization deactivated successfully."
                    };
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
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

