using Data.Entities.Base;
using Data.Enum;
using System;
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

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Campaign")]
        public int? CampaignId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        public string? QueueJobId { get; set; } // Redis queue job ID for tracking

        public ComparisonStatusEnum Status { get; set; } = ComparisonStatusEnum.Pending;

        [Column(TypeName = "jsonb")]
        public string? ResultJson { get; set; }

        public string? ErrorMessage { get; set; } // Error message if comparison failed

        public DateTime? ProcessedAt { get; set; } // When AI finished processing

        // Navigation
        public Job Job { get; set; } = null!;
        public Campaign? Campaign { get; set; }
        public Company Company { get; set; } = null!;
        public ICollection<ApplicationComparison> ApplicationComparisons { get; set; } = new List<ApplicationComparison>();
    }
}
