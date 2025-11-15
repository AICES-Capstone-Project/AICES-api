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
    [Table("AIScoreDetail")]
    public class AIScoreDetail : BaseEntity
    {
        [Key]
        public int ScoreDetailId { get; set; }

        [ForeignKey("Criteria")]
        public int CriteriaId { get; set; }

        [ForeignKey("AIScores")]
        public int ScoreId { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Matched { get; set; } // Percentage of resume match with this criterion (0-100)

        [Column(TypeName = "decimal(5,2)")]
        public decimal Score { get; set; } // Score for this specific criterion

        public string? AINote { get; set; } // AI note explaining why matched/not matched

        // Navigation
        public Criteria Criteria { get; set; } = null!;
        public AIScores AIScores { get; set; } = null!;
    }
}


