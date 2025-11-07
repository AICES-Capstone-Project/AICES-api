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
    [Table("ParsedResumes")]
    public class ParsedResumes : BaseEntity
    {
        [Key]
        public int ResumeId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        public string? FileUrl { get; set; } // URL to the uploaded resume file

        [Column(TypeName = "jsonb")]
        public string? Data { get; set; } // Raw parsed JSON data from resume parser

        public ResumeStatusEnum ResumeStatus { get; set; } // Status of the resume parsing
        // Navigation
        public Company Company { get; set; } = null!;
        public Job Job { get; set; } = null!;
        public ParsedCandidates? ParsedCandidates { get; set; }
    }
}


