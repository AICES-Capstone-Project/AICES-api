using System.Collections.Generic;

namespace Data.Models.Response
{
    public class SystemUserStatsResponse
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public List<UserRoleCount> ByRole { get; set; } = new();
    }

    public class UserRoleCount
    {
        public string Role { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

