using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class EmploymentTypeRequest
    {
        [Required(ErrorMessage = "Employment type name is required")]
        [MaxLength(255, ErrorMessage = "Employment type name cannot exceed 255 characters")]
        [DefaultValue("Full-time")]
        public string Name { get; set; } = string.Empty;
    }
}
