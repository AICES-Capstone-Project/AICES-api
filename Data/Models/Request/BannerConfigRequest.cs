using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class BannerConfigRequest
    {
        [Required(ErrorMessage = "Banner name is required")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [MaxLength(50)]
        public string? ColorCode { get; set; }

        public string? Source { get; set; }

        // Image file upload
        public IFormFile? ImageFile { get; set; }
    }
}
