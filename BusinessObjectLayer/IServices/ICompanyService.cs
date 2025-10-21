using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICompanyService
    {
        Task<ServiceResponse> GetAllAsync();
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> CreateAsync(CompanyRequest request);
        Task<ServiceResponse> UpdateAsync(int id, CompanyRequest request);
        Task<ServiceResponse> DeleteAsync(int id);
        Task<ServiceResponse> ApproveOrRejectAsync(int companyId, bool isApproved);

    }
}
