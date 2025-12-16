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
    public class EmploymentTypeService : IEmploymentTypeService
    {
        private readonly IUnitOfWork _uow;

        public EmploymentTypeService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
            var (list, total) = await employmentTypeRepo.GetPagedAsync(page, pageSize, search);

            var data = list
                .OrderBy(e => e.EmployTypeId)
                .Select(e => new EmploymentTypeResponse
                {
                    EmployTypeId = e.EmployTypeId,
                    Name = e.Name,
                    CreatedAt = e.CreatedAt
                }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Employment types retrieved successfully.",
                Data = new Data.Models.Response.Pagination.PaginatedEmploymentTypeResponse
                {
                    EmploymentTypes = data,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total
                }
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
            var item = await employmentTypeRepo.GetByIdAsync(id);
            if (item == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Data = new EmploymentTypeResponse
                {
                    EmployTypeId = item.EmployTypeId,
                    Name = item.Name,
                    CreatedAt = item.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(EmploymentTypeRequest request)
        {
            var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
            
            if (await employmentTypeRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var newItem = new EmploymentType
                {
                    Name = request.Name
                };

                await employmentTypeRepo.AddAsync(newItem);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Employment type created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, EmploymentTypeRequest request)
        {
            var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
            var item = await employmentTypeRepo.GetForUpdateAsync(id);
            if (item == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrEmpty(request.Name))
                    item.Name = request.Name;

                employmentTypeRepo.Update(item);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Employment type updated successfully."
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
            var employmentTypeRepo = _uow.GetRepository<IEmploymentTypeRepository>();
            var item = await employmentTypeRepo.GetForUpdateAsync(id);
            if (item == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                item.IsActive = false;
                employmentTypeRepo.Update(item);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Employment type deactivated successfully."
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
