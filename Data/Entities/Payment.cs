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
    [Table("Payment")]
    public class Payment : BaseEntity
    {
        [Key]
        public int PaymentId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("CompanySubscription")]
        public int? ComSubId { get; set; }

        [MaxLength(500)]
        public string? InvoiceUrl { get; set; }

        public PaymentStatusEnum PaymentStatus { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
        public CompanySubscription? CompanySubscription { get; set; }
        public ICollection<Transaction>? Transactions { get; set; }
    }
}


