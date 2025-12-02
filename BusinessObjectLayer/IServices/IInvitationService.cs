using Data.Enum;
using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IInvitationService
    {
        // Manager sends invitation to a user by email
        Task<ServiceResponse> SendInvitationAsync(string email);
        
        // Manager gets list of invitations for their company
        Task<ServiceResponse> GetCompanyInvitationsAsync(InvitationStatusEnum? status = null, string? search = null);
        
        // Manager cancels an invitation
        Task<ServiceResponse> CancelInvitationAsync(int invitationId);
        
        // Recruiter accepts an invitation
        Task<ServiceResponse> AcceptInvitationAsync(int invitationId);
        
        // Recruiter declines an invitation
        Task<ServiceResponse> DeclineInvitationAsync(int invitationId);
    }
}

