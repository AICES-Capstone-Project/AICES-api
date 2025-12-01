using Data.Enum;
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
        Task<ServiceResponse> GetPublicAsync();
        Task<ServiceResponse> GetPublicByIdAsync(int id);
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CompanyStatusEnum? status = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> GetSelfCompanyAsync();
        Task<ServiceResponse> GetRejectedSelfCompanyAsync();
        Task<ServiceResponse> SelfCreateAsync(CompanyRequest request);
        Task<ServiceResponse> UpdateSelfCompanyAsync(CompanyRequest request);
        Task<ServiceResponse> CreateAsync(CompanyRequest request);
        Task<ServiceResponse> UpdateAsync(int id, CompanyRequest request);
        Task<ServiceResponse> UpdateCompanyProfileAsync(CompanyProfileUpdateRequest request);
        Task<ServiceResponse> DeleteAsync(int id);
        Task<ServiceResponse> UpdateCompanyStatusAsync(int companyId, CompanyStatusEnum status, string? rejectionReason = null);
        Task<ServiceResponse> CancelCompanyAsync();

    }
}
