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

        public async Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId)
        {
            try
            {
                // Create CompanyUser with null CompanyId (user chưa join company nào)
                var companyUser = new CompanyUser
                {
                    UserId = userId,
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

        public async Task<ServiceResponse> GetMembersByCompanyIdAsync(int companyId)
        {
            try
            {
                var members = await _companyUserRepository.GetMembersByCompanyIdAsync(companyId);

                var responses = members.Select(m => new CompanyMemberResponse
                {
                    ComUserId = m.ComUserId,
                    UserId = m.UserId,
                    Email = m.User?.Email ?? string.Empty,
                    RoleName = m.User?.Role?.RoleName ?? string.Empty,
                    FullName = m.User?.Profile?.FullName ?? string.Empty,
                    AvatarUrl = m.User?.Profile?.AvatarUrl,
                    PhoneNumber = m.User?.Profile?.PhoneNumber,
                    JoinStatus = m.JoinStatus,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company members retrieved successfully.",
                    Data = responses
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get company members error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving company members."
                };
            }
        }
    }
}
