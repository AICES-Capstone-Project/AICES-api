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
    [Table("CompanySubscription")]
    public class CompanySubscription : BaseEntity
    {
        [Key]
        public int ComSubId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("Subscription")]
        public int SubscriptionId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public SubscriptionStatusEnum SubscriptionStatus { get; set; }

        [MaxLength(255)]
        public string? StripeSubscriptionId { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
        public Subscription Subscription { get; set; } = null!;
        public ICollection<Payment>? Payments { get; set; }
    }
}


