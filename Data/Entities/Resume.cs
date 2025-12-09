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
    [Table("Resumes")]
    public class Resume : BaseEntity
    {
        [Key]
        public int ResumeId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Candidate")]
        public int? CandidateId { get; set; }

        public string? QueueJobId { get; set; }

        public string? FileUrl { get; set; } // URL to the uploaded resume file

        [Column(TypeName = "jsonb")]
        public string? Data { get; set; } // Raw parsed JSON data from resume parser

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TotalScore { get; set; } // Overall AI score (0-100)

        [Column(TypeName = "decimal(5,2)")]
        public decimal? AdjustedScore { get; set; } // HR adjusted score when not satisfied with AI score

        [Column(TypeName = "jsonb")]
        public string? AIExplanation { get; set; } // AI explanation of the scoring

        public bool IsLatest { get; set; } // Check if this is the latest resume

        public ResumeStatusEnum Status { get; set; } // Status of the resume parsing

        // Navigation
        public Company Company { get; set; } = null!;
        public Job Job { get; set; } = null!;
        public Candidate? Candidate { get; set; }
        public ICollection<ScoreDetail> ScoreDetails { get; set; } = new List<ScoreDetail>();
    }
}
