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
    [Table("Payment")]
    public class Payment : BaseEntity
    {
        [Key]
        public int PaymentId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string? PaymentMethod { get; set; } // CreditCard, PayPal, BankTransfer, etc.

        public string? InvoiceUrl { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
    }
}


