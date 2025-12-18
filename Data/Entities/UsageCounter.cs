using Data.Entities.Base;
using Data.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("UsageCounters")]
    public class UsageCounter : BaseEntity
    {
        [Key]
        public int UsageCounterId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("CompanySubscription")]
        public int? CompanySubscriptionId { get; set; } // Null for Free plan

        public UsageTypeEnum UsageType { get; set; } // Resume, Comparison

        public DateTime PeriodStartDate { get; set; }
        
        public DateTime PeriodEndDate { get; set; }

        public int Used { get; set; } = 0;       // Đã dùng
        
        public int Limit { get; set; }            // Giới hạn

        public DateTime? UpdatedAt { get; set; }  // Lần cuối update counter

        // Navigation
        public Company Company { get; set; } = null!;
        public CompanySubscription? CompanySubscription { get; set; }
    }
}
