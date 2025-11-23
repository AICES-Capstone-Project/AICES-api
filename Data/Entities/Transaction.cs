using Data.Entities.Base;
using Data.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("Transactions")]
    public class Transaction : BaseEntity
    {
        [Key]
        public int TransactionId { get; set; }

        [ForeignKey("Payment")]
        public int PaymentId { get; set; }

        public TransactionGatewayEnum Gateway { get; set; }

        [MaxLength(200)]
        public string? TransactionRef { get; set; }

        [MaxLength(200)]
        public string? GatewayTransNo { get; set; }

        public int Amount { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        [MaxLength(100)]
        public string? ResponseCode { get; set; }

        public string? ResponseMessage { get; set; }

        [MaxLength(50)]
        public string? BankCode { get; set; }

        [MaxLength(50)]
        public string? AccountNumber { get; set; }

        [MaxLength(200)]
        public string? PayerName { get; set; }

        public DateTime? TransactionTime { get; set; }

        // Navigation
        public Payment Payment { get; set; } = null!;
    }
}
