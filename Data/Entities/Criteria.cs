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
    [Table("Criteria")]
    public class Criteria : BaseEntity
    {
        [Key]
        public int CriteriaId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } // Weight for scoring (e.g., 0.25 for 25%)

        // Navigation
        public Job Job { get; set; } = null!;
        // Navigation: One Criteria -> Many ScoreDetails
        public ICollection<ScoreDetail> ScoreDetails { get; set; } = new List<ScoreDetail>();
    }
}


