using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class CategoryRequest
    {
        [Required(ErrorMessage = "Category name is required")]
        [MaxLength(255, ErrorMessage = "Category name cannot exceed 255 characters")]
        [DefaultValue("Software Development")]
        public string Name { get; set; } = string.Empty;
    }
}
