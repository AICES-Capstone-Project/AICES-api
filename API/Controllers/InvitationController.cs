using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/companies/invitations")]
    [ApiController]
    [Authorize]
    public class CompanyInvitationController : ControllerBase
    {
        private readonly IInvitationService _invitationService;

        public CompanyInvitationController(IInvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        /// <summary>
        /// Manager sends invitation to a user by email
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> SendInvitation([FromBody] SendInvitationRequest request)
        {
            var response = await _invitationService.SendInvitationAsync(request.Email);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Manager gets list of invitations for their company
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> GetCompanyInvitations(
            [FromQuery] InvitationStatusEnum? status = null,
            [FromQuery] string? search = null)
        {
            var response = await _invitationService.GetCompanyInvitationsAsync(status, search);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/invitations")]
    [ApiController]
    [Authorize]
    public class InvitationController : ControllerBase
    {
        private readonly IInvitationService _invitationService;

        public InvitationController(IInvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        /// <summary>
        /// Manager cancels an invitation
        /// </summary>
        [HttpDelete("{invitationId}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> CancelInvitation(int invitationId)
        {
            var response = await _invitationService.CancelInvitationAsync(invitationId);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Recruiter accepts an invitation
        /// </summary>
        [HttpPut("{invitationId}/accept")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> AcceptInvitation(int invitationId)
        {
            var response = await _invitationService.AcceptInvitationAsync(invitationId);
            return ControllerResponse.Response(response);
        }

        /// <summary>
        /// Recruiter declines an invitation
        /// </summary>
        [HttpPut("{invitationId}/decline")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> DeclineInvitation(int invitationId)
        {
            var response = await _invitationService.DeclineInvitationAsync(invitationId);
            return ControllerResponse.Response(response);
        }
    }
}

