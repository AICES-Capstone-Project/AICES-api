using Data.Entities.Base;
using System;
using Data.Enum;
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
        public JobStatusEnum JobStatus { get; set; }
        
        [ForeignKey("Specialization")]
        public int? SpecializationId { get; set; }

        // Navigation
        public CompanyUser CompanyUser { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public Specialization? Specialization { get; set; }
        public ICollection<JobEmploymentType>? JobEmploymentTypes { get; set; }
        public ICollection<Criteria>? Criteria { get; set; }
        public ICollection<ParsedCandidates>? ParsedCandidates { get; set; }
        public ICollection<ParsedResumes>? ParsedResumes { get; set; }
        public ICollection<RankingResults>? RankingResults { get; set; }
        public ICollection<Reports>? Reports { get; set; }
        public ICollection<JobSkill>? JobSkills { get; set; }
    }
}
