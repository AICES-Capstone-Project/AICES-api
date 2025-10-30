using Data.Enum;

namespace Data.Models.Request
{
    public class UpdateJoinStatusRequest
    {
        public JoinStatusEnum JoinStatus { get; set; }
        public string? RejectionReason { get; set; }
    }
}



