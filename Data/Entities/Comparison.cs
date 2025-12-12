using Data.Entities.Base;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("Comparisons")]
    public class Comparison : BaseEntity
    {
        [Key]
        public int ComparisonId { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ResultJson { get; set; }

        // Navigation
        public ICollection<ApplicationComparison> ApplicationComparisons { get; set; } = new List<ApplicationComparison>();
    }
}
