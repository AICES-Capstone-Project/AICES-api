using Data.Entities;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyRepository
    {
        Task<IEnumerable<Company>> GetAllAsync();
        Task<List<Company>> GetCompaniesAsync(int page, int pageSize, string? search = null);
        Task<List<CompanyResponse>> GetCompaniesWithCreatorAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalCompaniesAsync(string? search = null);
        Task<List<Company>> GetPublicCompaniesAsync();
        Task<Company?> GetPublicByIdAsync(int id);
        Task<Company?> GetByIdAsync(int id);
        Task<CompanyDetailResponse?> GetByIdWithCreatorAsync(int id);
        Task<Company?> GetForUpdateAsync(int id);
        Task<Company> AddAsync(Company company);
        Task UpdateAsync(Company company);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsAsync(int companyId);
        Task<bool> UpdateUserRoleByCompanyAsync(int companyId, string newRoleName);

    }
}
