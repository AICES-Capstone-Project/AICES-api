using Data.Entities;
using Data.Enum;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories
{
    public class InvitationRepository : IInvitationRepository
    {
        private readonly AICESDbContext _context;

        public InvitationRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Invitation invitation)
        {
            await _context.Invitations.AddAsync(invitation);
        }

        public async Task<Invitation?> GetByIdAsync(int invitationId)
        {
            return await _context.Invitations
                .AsNoTracking()
                .Include(i => i.Sender)
                    .ThenInclude(s => s.Profile)
                .Include(i => i.Receiver)
                    .ThenInclude(r => r.Profile)
                .Include(i => i.Company)
                .FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.IsActive);
        }

        public async Task<Invitation?> GetForUpdateAsync(int invitationId)
        {
            return await _context.Invitations
                .Include(i => i.Sender)
                    .ThenInclude(s => s.Profile)
                .Include(i => i.Receiver)
                    .ThenInclude(r => r.Profile)
                .Include(i => i.Receiver)
                    .ThenInclude(r => r.CompanyUser)
                .Include(i => i.Company)
                .FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.IsActive);
        }

        public async Task UpdateAsync(Invitation invitation)
        {
            _context.Invitations.Update(invitation);
        }

        public async Task<IEnumerable<Invitation>> GetByCompanyIdAsync(int companyId, InvitationStatusEnum? status = null, string? search = null)
        {
            var query = _context.Invitations
                .AsNoTracking()
                .Include(i => i.Sender)
                    .ThenInclude(s => s.Profile)
                .Include(i => i.Receiver)
                    .ThenInclude(r => r.Profile)
                .Include(i => i.Company)
                .Where(i => i.CompanyId == companyId && i.IsActive);

            if (status.HasValue)
            {
                query = query.Where(i => i.InvitationStatus == status.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i => i.Email.Contains(search) || 
                    (i.Receiver.Profile != null && i.Receiver.Profile.FullName != null && i.Receiver.Profile.FullName.Contains(search)));
            }

            return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<Invitation?> GetPendingByReceiverIdAndCompanyIdAsync(int receiverId, int companyId)
        {
            return await _context.Invitations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ReceiverId == receiverId 
                    && i.CompanyId == companyId 
                    && i.InvitationStatus == InvitationStatusEnum.Pending 
                    && i.IsActive);
        }

        public async Task<IEnumerable<Invitation>> GetPendingByReceiverIdAsync(int receiverId)
        {
            return await _context.Invitations
                .AsNoTracking()
                .Include(i => i.Sender)
                    .ThenInclude(s => s.Profile)
                .Include(i => i.Company)
                .Where(i => i.ReceiverId == receiverId 
                    && i.InvitationStatus == InvitationStatusEnum.Pending 
                    && i.IsActive)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasPendingInvitationAsync(int receiverId, int companyId)
        {
            return await _context.Invitations
                .AsNoTracking()
                .AnyAsync(i => i.ReceiverId == receiverId 
                    && i.CompanyId == companyId 
                    && i.InvitationStatus == InvitationStatusEnum.Pending 
                    && i.IsActive);
        }
    }
}

