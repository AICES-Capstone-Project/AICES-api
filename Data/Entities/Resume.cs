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

        [ForeignKey("Candidate")]
        public int? CandidateId { get; set; }

        public string? FileUrl { get; set; } // URL to the uploaded resume file
        public string? FileHash { get; set; } // Hash of the uploaded resume file

        [Column(TypeName = "jsonb")]
        public string? Data { get; set; } // Raw parsed JSON data from resume parser

        public bool? IsLatest { get; set; } // Check if this is the latest resume

        public ResumeStatusEnum Status { get; set; } // Status of the resume parsing

        // Usage statistics for analytics
        public int ReuseCount { get; set; } = 0; // Number of times this resume was reused
        public DateTime? LastReusedAt { get; set; } // Last time this resume was reused

        // Navigation
        public Company Company { get; set; } = null!;
        public Candidate? Candidate { get; set; }
        public ICollection<ResumeApplication> ResumeApplications { get; set; } = new List<ResumeApplication>();
    }
}
