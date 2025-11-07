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
    [Table("RankingResults")]
    public class RankingResults : BaseEntity
    {
        [Key]
        public int RankingId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("ParsedCandidates")]
        public int CandidateId { get; set; }

        [ForeignKey("AIScores")]
        public int ScoreId { get; set; }

        public int RankPosition { get; set; } // Position in ranking (1 = best)

        // Navigation
        public Job Job { get; set; } = null!;
        public ParsedCandidates ParsedCandidate { get; set; } = null!;
    }
}


