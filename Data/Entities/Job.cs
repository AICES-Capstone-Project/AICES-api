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

        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        [ForeignKey("CompanyUser")]
        public int ComUserId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Slug { get; set; }

        public string? Requirements { get; set; }

        [MaxLength(50)]
        public string? WorkType { get; set; }

        [MaxLength(100)]
        public string? Duration { get; set; }

        [MaxLength(255)]
        public string? JobLevels { get; set; }

        [MaxLength(255)]
        public string? SalaryRange { get; set; }

        public int NumberPositions { get; set; } = 1;

        // Navigation
        public Category Category { get; set; } = null!;
        public CompanyUser CompanyUser { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<Criteria>? Criterias { get; set; }
        public ICollection<ParsedCandidates>? ParsedCandidates { get; set; }
        public ICollection<ParsedResumes>? ParsedResumes { get; set; }
        public ICollection<RankingResults>? RankingResults { get; set; }
        public ICollection<Reports>? Reports { get; set; }
    }
}
