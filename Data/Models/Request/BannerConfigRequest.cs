using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class BannerConfigRequest
    {
        [Required(ErrorMessage = "Title is required")]
        [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        [DefaultValue("Welcome Banner")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Color code cannot exceed 50 characters")]
        public string? ColorCode { get; set; }

        // Banner image file upload
        public IFormFile? Source { get; set; }
    }
}
