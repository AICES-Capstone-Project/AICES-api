using System;
using Data.Enum;

namespace Data.Models.Response
{
    public class CompanyMemberResponse
    {
        public int ComUserId { get; set; }
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? RoleName { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public JoinStatusEnum JoinStatus { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}


