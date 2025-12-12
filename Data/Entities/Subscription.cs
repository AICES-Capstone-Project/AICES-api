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
    [Table("Subscriptions")]
    public class Subscription : BaseEntity
    {
        [Key]
        public int SubscriptionId { get; set; }

        [MaxLength(255)]
        public string? StripePriceId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int Price { get; set; }

        public DurationEnum Duration { get; set; }
    
        public int ResumeLimit { get; set; } 
        
        public int HoursLimit { get; set; }
        
        public int? CompareLimit { get; set; } 
        
        public int? CompareHoursLimit { get; set; }

        // Navigation
        public ICollection<CompanySubscription>? CompanySubscriptions { get; set; }
    }
}


