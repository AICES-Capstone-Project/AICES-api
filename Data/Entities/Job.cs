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

        // Track who created the job
        [ForeignKey("Creator")]
        public int? CreatedBy { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Slug { get; set; }

        public string? Requirements { get; set; }
        public JobStatusEnum JobStatus { get; set; }
        
        [ForeignKey("Specialization")]
        public int? SpecializationId { get; set; }

        [ForeignKey("Level")]
        public int? LevelId { get; set; }

        // Track if job is in any campaign
        public bool IsInCampaign { get; set; } = false;

        // Navigation
        public CompanyUser CompanyUser { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public User? Creator { get; set; }
        public Specialization? Specialization { get; set; }
        public Level? Level { get; set; }
        public ICollection<JobEmploymentType>? JobEmploymentTypes { get; set; }
        public ICollection<Criteria>? Criteria { get; set; }
        public ICollection<ResumeApplication>? ResumeApplications { get; set; }
        public ICollection<JobSkill>? JobSkills { get; set; }
        public ICollection<JobLanguage>? JobLanguages { get; set; }
        public ICollection<JobCampaign>? JobCampaigns { get; set; }
        public ICollection<Comparison>? Comparisons { get; set; }
    }
}
