using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/companies")]
    [ApiController]
    [Authorize]
    public class CompanyUserController : ControllerBase
    {
        private readonly ICompanyUserService _companyUserService;

        public CompanyUserController(ICompanyUserService companyUserService)
        {
            _companyUserService = companyUserService;
        }

        [HttpGet("members")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyMembers()
        {
            var response = await _companyUserService.GetSelfCompanyMembersAsync();
            return ControllerResponse.Response(response);
        }

        // Recruiter sends join request
        [HttpPost("{companyId}/join")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> SendJoinRequest(int companyId)
        {
            var response = await _companyUserService.SendJoinRequestAsync(companyId);
            return ControllerResponse.Response(response);
        }

        // Manager views pending join requests
        [HttpGet("join-requests")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetPendingJoinRequests()
        {
            var response = await _companyUserService.GetPendingJoinRequestsSelfAsync();
            return ControllerResponse.Response(response);
        }

        // Manager approves or rejects join request
        [HttpPut("join-requests/{comUserId}/status")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> UpdateJoinRequestStatus(int comUserId, [FromBody] UpdateJoinStatusRequest request)
        {
            var response = await _companyUserService.UpdateJoinRequestStatusSelfAsync(comUserId, request.JoinStatus);
            return ControllerResponse.Response(response);
        }

        // Cancel own join request (only if status is Pending)
        [HttpDelete("join-request/cancel")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> CancelJoinRequest()
        {
            var response = await _companyUserService.CancelJoinRequestAsync();
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/companies")]
    [ApiController]
    [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
    public class SystemCompanyUserController : ControllerBase
    {
        private readonly ICompanyUserService _companyUserService;

        public SystemCompanyUserController(ICompanyUserService companyUserService)
        {
            _companyUserService = companyUserService;
        }

        [HttpGet("{companyId}/members")]
        public async Task<IActionResult> GetCompanyMembers(int companyId)
        {
            var response = await _companyUserService.GetMembersByCompanyIdAsync(companyId);
            return ControllerResponse.Response(response);
        }
    }
}


