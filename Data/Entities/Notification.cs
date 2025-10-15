using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("Notification")]
    public class Notification : BaseEntity
    {
        [Key]
        public int NotifId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; } // Info, Warning, Success, Error

        public string? Message { get; set; }

        public string? Detail { get; set; }

        public bool IsRead { get; set; } = false;

        // Navigation
        public User User { get; set; } = null!;
    }
}


