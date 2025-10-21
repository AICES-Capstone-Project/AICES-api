using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class ReportRequest
    {
        [Required(ErrorMessage = "Job ID is required")]
        public int JobId { get; set; }

        [Required(ErrorMessage = "User ID is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Report type is required")]
        public ReportTypeEnum Type { get; set; }

        [MaxLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Export format is required")]
        public ExportFormatEnum ExportFormat { get; set; }

        public string? Data { get; set; }
    }
}
