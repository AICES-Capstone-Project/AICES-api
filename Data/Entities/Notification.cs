using Data.Entities.Base;
using Data.Enum;
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

        public NotificationTypeEnum Type { get; set; }

        public string? Message { get; set; }

        public string? Detail { get; set; }

        public bool IsRead { get; set; } = false;

        // Navigation
        public User User { get; set; } = null!;
    }
}


