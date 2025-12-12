using Data.Entities.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("ApplicationComparisons")]
    public class ApplicationComparison : BaseEntity
    {
        [ForeignKey("ResumeApplication")]
        public int ApplicationId { get; set; }

        [ForeignKey("Comparison")]
        public int ComparisonId { get; set; }

        // Navigation
        public ResumeApplication ResumeApplication { get; set; } = null!;
        public Comparison Comparison { get; set; } = null!;
    }
}
