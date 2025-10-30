using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICompanyUserService
    {
        Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId);
        Task<ServiceResponse> GetMembersByCompanyIdAsync(int companyId);
        Task<ServiceResponse> SendJoinRequestAsync(int companyId);
        Task<ServiceResponse> GetPendingJoinRequestsAsync(int companyId);
        Task<ServiceResponse> UpdateJoinRequestStatusAsync(int companyId, int comUserId, Data.Enum.JoinStatusEnum joinStatus);
        Task<ServiceResponse> GetSelfCompanyMembersAsync();
        Task<ServiceResponse> GetPendingJoinRequestsSelfAsync();
        Task<ServiceResponse> UpdateJoinRequestStatusSelfAsync(int comUserId, Data.Enum.JoinStatusEnum joinStatus);
    }
}