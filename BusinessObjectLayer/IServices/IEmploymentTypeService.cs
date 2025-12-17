using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IEmploymentTypeService
    {
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> CreateAsync(EmploymentTypeRequest request);
        Task<ServiceResponse> UpdateAsync(int id, EmploymentTypeRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
    }
}
