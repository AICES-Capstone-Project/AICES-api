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
    public class CompanyRequest
    {
        [Required(ErrorMessage = "Company name is required")]
        [MaxLength(255, ErrorMessage = "Company name cannot exceed 255 characters")]
        [DefaultValue("Tech Solutions Inc.")]
        public string Name { get; set; } = string.Empty;

        [DefaultValue("We are a leading technology company specializing in software development and IT consulting services.")]
        public string? Description { get; set; }

        [DefaultValue("123 Tech Street, Silicon Valley, CA 94025")]
        public string? Address { get; set; }

        [DefaultValue("https://www.techsolutions.com")]
        public string? Website { get; set; }

        // Logo file upload
        public IFormFile? LogoFile { get; set; }

        // at least 1
        [Required(ErrorMessage = "At least one document file is required")]
        [MinLength(1, ErrorMessage = "At least one document must be provided")]
        public List<IFormFile>? DocumentFiles { get; set; }
        
        // at least 1
        [Required(ErrorMessage = "Document types are required")]
        [MinLength(1, ErrorMessage = "At least one document type must be provided")]
        [DefaultValue(new string[] { "Business License" })]
        public List<string>? DocumentTypes { get; set; }
    }
}
