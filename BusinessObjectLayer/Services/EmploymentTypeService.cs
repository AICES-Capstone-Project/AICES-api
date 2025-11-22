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
    public class EmploymentTypeService : IEmploymentTypeService
    {
        private readonly IEmploymentTypeRepository _repository;

        public EmploymentTypeService(IEmploymentTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var list = await _repository.GetAllAsync();

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
                Data = data
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id);
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
            if (await _repository.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type name already exists."
                };
            }

            var newItem = new EmploymentType
            {
                Name = request.Name
            };

            await _repository.AddAsync(newItem);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Employment type created successfully."
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, EmploymentTypeRequest request)
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type not found."
                };
            }

            if (!string.IsNullOrEmpty(request.Name))
                item.Name = request.Name;

            await _repository.UpdateAsync(item);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Employment type updated successfully."
            };
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Employment type not found."
                };
            }

            item.IsActive = false;
            await _repository.UpdateAsync(item);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Employment type deactivated successfully."
            };
        }
    }
}
