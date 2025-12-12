using Data.Entities.Base;
using Data.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("ResumeApplications")]
    public class ResumeApplication : BaseEntity
    {
        [Key]
        public int ApplicationId { get; set; }

        [ForeignKey("Resume")]
        public int ResumeId { get; set; }

        [ForeignKey("Campaign")]
        public int? CampaignId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Candidate")]
        public int? CandidateId { get; set; }

        public string? QueueJobId { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TotalScore { get; set; } // Overall AI score (0-100)

        [Column(TypeName = "decimal(5,2)")]
        public decimal? AdjustedScore { get; set; } // HR adjusted score when not satisfied with AI score
        [Column(TypeName = "jsonb")]
        public string? AIExplanation { get; set; } // AI explanation of the scoring
        public string? RequiredSkills { get; set; } // Required skills for the job
        public string? MatchSkills { get; set; }
        public string? MissingSkills { get; set; }
        public ApplicationStatusEnum Status { get; set; }

        // Navigation
        public Resume Resume { get; set; } = null!;
        public Campaign? Campaign { get; set; }
        public Job Job { get; set; } = null!;
        public Candidate? Candidate { get; set; }
        public ICollection<ScoreDetail> ScoreDetails { get; set; } = new List<ScoreDetail>();
        public ICollection<ApplicationComparison> ApplicationComparisons { get; set; } = new List<ApplicationComparison>();
    }
}

