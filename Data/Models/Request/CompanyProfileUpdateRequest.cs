using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class CompanyProfileUpdateRequest
    {
        public string? Description { get; set; }

        public string? Address { get; set; }

        public string? Website { get; set; }

        public IFormFile? LogoFile { get; set; }
    }
}

