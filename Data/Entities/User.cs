using Data.Entities.Base;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Data.Entities
{
    [Table("Users")]
    public class User : BaseEntity
    {
        [Key]
        public int UserId { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Password { get; set; } // với social login có thể null
        public UserStatusEnum Status { get; set; }

        // Navigation
        [ForeignKey("Role")]
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public Profile? Profile { get; set; }
        public CompanyUser? CompanyUser { get; set; }
        public ICollection<LoginProvider>? LoginProviders { get; set; }
        public ICollection<RefreshToken>? RefreshTokens { get; set; }
        public ICollection<Notification>? Notifications { get; set; }
        public ICollection<Blog>? Blogs { get; set; }
        public ICollection<Invitation>? SentInvitations { get; set; }
        public ICollection<Invitation>? ReceivedInvitations { get; set; }
        public ICollection<Job>? CreatedJobs { get; set; }
        public ICollection<Campaign>? CreatedCampaigns { get; set; }
    }
}
