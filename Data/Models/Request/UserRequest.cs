using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Enum;

namespace Data.Models.Request
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MinLength(5, ErrorMessage = "Email must be at least 5 characters.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        [DefaultValue("user@example.com")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [MaxLength(100, ErrorMessage = "Password cannot exceed 100 characters.")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
            ErrorMessage = "Password must include one uppercase, one lowercase, one number, and one special character.")]
        [DefaultValue("Abc@12345")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Role ID is required.")]
        [DefaultValue(5)]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        [DefaultValue("Nguyen Van FPT")]
        public string? FullName { get; set; }
    }

    public class UpdateUserRequest
    {
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [MaxLength(100, ErrorMessage = "Password cannot exceed 100 characters.")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
            ErrorMessage = "Password must include one uppercase, one lowercase, one number, and one special character.")]
        [DefaultValue("Abc@12345")]
        public string? Password { get; set; }

        [DefaultValue(5)]
        public int? RoleId { get; set; }
    }

    public class UpdateUserStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        public UserStatusEnum Status { get; set; }
    }
}
