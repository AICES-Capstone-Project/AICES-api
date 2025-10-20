using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class CriteriaRequest
    {
        [Required(ErrorMessage = "Criteria name is required")]
        [MaxLength(255, ErrorMessage = "Criteria name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 1.00, ErrorMessage = "Weight must be between 0.01 and 1.00")]
        public decimal Weight { get; set; }
    }
}
