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
    [Table("Companies")]
    public class Company : BaseEntity
    {
        [Key]
        public int CompanyId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Address { get; set; }
        
        [MaxLength(255)]
        public string? TaxCode { get; set; }

        [MaxLength(255)]
        public string? Website { get; set; }

        public string? LogoUrl { get; set; }

        public CompanyStatusEnum CompanyStatus { get; set; }
        [ForeignKey("CompanyUser")]
        public int CreatedBy { get; set; }
        [ForeignKey("User")]
        public int? ApprovedBy { get; set; }
        [MaxLength(255)]
        public string? RejectReason { get; set; }

        [MaxLength(255)]
        public string? StripeCustomerId { get; set; }

        // Navigation
        public ICollection<CompanyUser>? CompanyUsers { get; set; }
        public ICollection<Job>? Jobs { get; set; }
        public ICollection<CompanySubscription>? CompanySubscriptions { get; set; }
        public ICollection<Resume>? Resumes { get; set; }
        public ICollection<Payment>? Payments { get; set; }
        public ICollection<CompanyDocument>? CompanyDocuments { get; set; }
        public ICollection<Invitation>? Invitations { get; set; }
        public ICollection<Campaign>? Campaigns { get; set; }
    }
}
