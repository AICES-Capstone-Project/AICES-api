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
    [Table("Reports")]
    public class Reports : BaseEntity
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public ReportTypeEnum Type { get; set; }

        [MaxLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public ExportFormatEnum ExportFormat { get; set; }

        public string? Data { get; set; } // JSON or serialized data

        // Navigation
        public Job Job { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}


