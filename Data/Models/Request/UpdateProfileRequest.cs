using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class UpdateProfileRequest
    {
        [MaxLength(255)]
        public string? FullName { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public IFormFile? AvatarFile { get; set; } 
    }
}
