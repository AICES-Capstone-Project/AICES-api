using Data.Enum;

namespace Data.Models.Response
{
    public class InvitationResponse
    {
        public int InvitationId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? ReceiverName { get; set; }
        public string? SenderName { get; set; }
        public string? CompanyName { get; set; }
        public int? CompanyId { get; set; }
        public InvitationStatusEnum Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class InvitationDetailResponse
    {
        public int InvitationId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public InvitationStatusEnum Status { get; set; }
    }

    public class NotificationWithInvitationResponse
    {
        public int NotifId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? Detail { get; set; }
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
        public InvitationDetailResponse? Invitation { get; set; }
    }
}

