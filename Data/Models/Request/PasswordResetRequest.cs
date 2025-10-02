using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class PasswordResetRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MinLength(5, ErrorMessage = "Email must be at least 5 characters.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        [DefaultValue("user@example.com")]
        public string Email { get; set; }
    }
}
