using Data.Entities;
using System.Collections.Generic;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyUserRepository
    {
        Task<CompanyUser> AddCompanyUserAsync(CompanyUser companyUser);
        Task<CompanyUser?> GetByUserIdAsync(int userId);
        Task<CompanyUser?> GetCompanyUserByUserIdAsync(int userId);
        Task<bool> ExistsAsync(int comUserId);
        Task UpdateAsync(CompanyUser companyUser);
        Task<List<CompanyUser>> GetMembersByCompanyIdAsync(int companyId);
    }
}
