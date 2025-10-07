using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class UserResponse
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string RoleName { get; set; }
        public string FullName { get; set; }
        public string Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string AvatarUrl { get; set; }
        public string PhoneNumber { get; set; }
        public List<LoginProviderInfo> LoginProviders { get; set; } = new List<LoginProviderInfo>();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LoginProviderInfo
    {
        public AuthProviderEnum AuthProvider { get; set; }
        public string ProviderId { get; set; }
        public bool IsActive { get; set; }
    }
}
