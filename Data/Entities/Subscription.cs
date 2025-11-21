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
    [Table("Subscriptions")]
    public class Subscription : BaseEntity
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int DurationDays { get; set; }

        public string? Limit { get; set; } // Number of resume screenings or other limit

        public string StripePriceId { get; set; } = string.Empty;

        // Navigation
        public ICollection<CompanySubscription>? CompanySubscriptions { get; set; }
    }
}


