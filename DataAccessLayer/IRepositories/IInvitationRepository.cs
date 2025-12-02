using Data.Entities;
using Data.Enum;

namespace DataAccessLayer.IRepositories
{
    public interface IInvitationRepository
    {
        Task AddAsync(Invitation invitation);
        Task<Invitation?> GetByIdAsync(int invitationId);
        Task<Invitation?> GetForUpdateAsync(int invitationId);
        Task UpdateAsync(Invitation invitation);
        Task<IEnumerable<Invitation>> GetByCompanyIdAsync(int companyId, InvitationStatusEnum? status = null, string? search = null);
        Task<Invitation?> GetPendingByReceiverIdAndCompanyIdAsync(int receiverId, int companyId);
        Task<IEnumerable<Invitation>> GetPendingByReceiverIdAsync(int receiverId);
        Task<bool> HasPendingInvitationAsync(int receiverId, int companyId);
    }
}

