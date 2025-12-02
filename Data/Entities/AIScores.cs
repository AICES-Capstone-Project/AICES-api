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
    [Table("AIScores")]
    public class AIScores : BaseEntity
    {
        [Key]
        public int ScoreId { get; set; }

        [ForeignKey("ParsedCandidates")]
        public int CandidateId { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TotalResumeScore { get; set; } // Overall score (0-100)

        public string? AIExplanation { get; set; } // AI explanation of the scoring

        // Navigation
        public ICollection<AIScoreDetail>? AIScoreDetails { get; set; }
        public ParsedCandidates ParsedCandidate { get; set; } = null!;
    }
}


