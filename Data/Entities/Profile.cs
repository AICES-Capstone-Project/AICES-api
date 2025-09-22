using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("Profiles")]
    public class Profile : BaseEntity
    {
        [Key]
        public int ProfileId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [MaxLength(255)]
        public string? FullName { get; set; }

        public string? Address { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? AvatarUrl { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        // Navigation
        public User? User { get; set; }
    }
}
