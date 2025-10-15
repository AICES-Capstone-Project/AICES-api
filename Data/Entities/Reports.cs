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
    [Table("Reports")]
    public class Reports : BaseEntity
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; } // PDF, Excel, CSV, etc.

        [MaxLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [MaxLength(50)]
        public string? ExportFormat { get; set; }

        public string? Data { get; set; } // JSON or serialized data

        // Navigation
        public Job Job { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}


