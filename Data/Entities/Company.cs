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
        public string? Website { get; set; }

        public string? LogoUrl { get; set; }

        // Navigation
        public ICollection<CompanyMember>? CompanyMembers { get; set; }
        public ICollection<Job>? Jobs { get; set; }
    }
}
