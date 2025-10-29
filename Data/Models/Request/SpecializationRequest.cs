using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class SpecializationRequest
    {
        [Required(ErrorMessage = "Specialization name is required")]
        [MaxLength(255, ErrorMessage = "Specialization name cannot exceed 255 characters")]
        [DefaultValue("Full-Stack Development")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category ID is required")]
        [DefaultValue(1)]
        public int CategoryId { get; set; }
    }
}

