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
    [Table("Jobs")]
    public class Job : BaseEntity
    {
        [Key]
        public int JobId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("CompanyMember")]
        public int ComMemId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Slug { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [ForeignKey("User")]
        public int CreatedBy { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
        public CompanyMember CompanyMember { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<JobPosition>? JobPositions { get; set; }
    }
}
