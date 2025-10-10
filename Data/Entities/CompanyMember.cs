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
    [Table("CompanyMembers")]
    public class CompanyMember : BaseEntity
    {
        [Key]
        public int ComMemId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [MaxLength(255)]
        public string? PositionTitle { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<Job>? Jobs { get; set; }
    }
}
