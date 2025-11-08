using Data.Entities;
using System.Collections.Generic;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyUserRepository
    {
        Task<CompanyUser> AddCompanyUserAsync(CompanyUser companyUser);
        Task<CompanyUser?> GetByUserIdAsync(int userId);
        Task<CompanyUser?> GetCompanyUserByUserIdAsync(int userId);
        Task<CompanyUser?> GetByComUserIdAsync(int comUserId);
        Task<bool> ExistsAsync(int comUserId);
        Task UpdateAsync(CompanyUser companyUser);
        Task<List<CompanyUser>> GetMembersByCompanyIdAsync(int companyId);
        Task<List<CompanyUser>> GetPendingByCompanyIdAsync(int companyId);
        Task<List<CompanyUser>> GetApprovedAndInvitedMembersByCompanyIdAsync(int companyId);
    }
}
