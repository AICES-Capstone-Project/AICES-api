using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanyUserService : ICompanyUserService
    {
        private readonly ICompanyUserRepository _companyUserRepository;

        public CompanyUserService(ICompanyUserRepository companyUserRepository)
        {
            _companyUserRepository = companyUserRepository;
        }

        public async Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId, int userRoleId)
        {
            try
            {
                // Create CompanyUser with null CompanyId (user chưa join company nào)
                var companyUser = new CompanyUser
                {
                    UserId = userId,
                    RoleId = userRoleId,
                    JoinStatus = JoinStatusEnum.NotApplied,
                    IsActive = true
                };
                
                await _companyUserRepository.AddCompanyUserAsync(companyUser);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Default company user created successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating company user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Failed to create default company user."
                };
            }
        }
    }
}
