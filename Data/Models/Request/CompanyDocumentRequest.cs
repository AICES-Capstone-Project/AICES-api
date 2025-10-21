using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class CompanyDocumentRequest
    {
        [Required(ErrorMessage = "Company ID is required")]
        public int CompanyId { get; set; }

        [MaxLength(100)]
        public string? DocumentType { get; set; }

        // File upload
        public IFormFile? DocumentFile { get; set; }
    }
}
