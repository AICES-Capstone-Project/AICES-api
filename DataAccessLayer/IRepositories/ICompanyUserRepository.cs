using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyUserRepository
    {
        Task<CompanyUser> AddCompanyUserAsync(CompanyUser companyUser);
    }
}
