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
        
        public bool IsAdjusted { get; set; } = false; // Flag to indicate if score has been adjusted
        
        [ForeignKey("AdjustedByUser")]
        public int? AdjustedBy { get; set; } // UserId of the last person who modified the score
        
        [Column(TypeName = "jsonb")]
        public string? AIExplanation { get; set; } // AI explanation of the scoring
        public string? RequiredSkills { get; set; } // Required skills for the job
        public string? MatchSkills { get; set; }
        public string? MissingSkills { get; set; }
        public ApplicationStatusEnum Status { get; set; }
        public ApplicationErrorEnum? ErrorType { get; set; } // Detailed error type from AI or system
        public string? Note { get; set; } // Note for the application
        public string? ErrorMessage { get; set; } // Error message if parsing failed

        // Tracking and logging fields
        [ForeignKey("ClonedFromApplication")]
        public int? ClonedFromApplicationId { get; set; } // FK to original application if cloned
        public ProcessingModeEnum? ProcessingMode { get; set; } // Processing mode: Parse, Score, Clone, Rescore
        public DateTime? ProcessedAt { get; set; } // When AI finished processing (or when cloned)
        public int? ProcessingTimeMs { get; set; } // AI processing time in milliseconds
        public DateTime? HiredAt { get; set; } // When the application was hired
        public DateTime? RejectedAt { get; set; } // When the application was rejected

        // Navigation
        public Resume Resume { get; set; } = null!;
        public Campaign? Campaign { get; set; }
        public Job Job { get; set; } = null!;
        public Candidate? Candidate { get; set; }
        public User? AdjustedByUser { get; set; }
        public ICollection<ScoreDetail> ScoreDetails { get; set; } = new List<ScoreDetail>();
        public ICollection<ApplicationComparison> ApplicationComparisons { get; set; } = new List<ApplicationComparison>();
    }
}

