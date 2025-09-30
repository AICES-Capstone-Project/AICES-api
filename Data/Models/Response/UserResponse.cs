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
        public string AvatarUrl { get; set; }
        public string FullName { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
    }
}
