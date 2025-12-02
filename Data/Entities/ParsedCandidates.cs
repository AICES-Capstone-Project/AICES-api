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
    [Table("ParsedCandidates")]
    public class ParsedCandidates : BaseEntity
    {
        [Key]
        public int CandidateId { get; set; }

        [ForeignKey("ParsedResumes")]
        public int ResumeId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [Required, MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        // Navigation
        public ParsedResumes ParsedResumes { get; set; } = null!;
        public Job Job { get; set; } = null!;
        public ICollection<AIScores> AIScores { get; set; } = new List<AIScores>();
        public RankingResults? RankingResult { get; set; }
    }
}


