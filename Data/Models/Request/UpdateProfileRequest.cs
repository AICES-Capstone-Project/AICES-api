using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class UpdateProfileRequest
    {
        [MaxLength(255, ErrorMessage = "Full name cannot exceed 255 characters.")]
        [RegularExpression(@"^[a-zA-ZÀ-ỹ\s'.-]+$", ErrorMessage = "Full name can only contain letters, spaces, and basic punctuation.")]
        [DefaultValue("John Doe")]
        public string? FullName { get; set; }

        [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        [DefaultValue("123 Cach mang thang 8, Ho Chi Minh City")]
        public string? Address { get; set; }

        [DataType(DataType.Date)]
        [DefaultValue("2003-01-01")]
        [CustomValidation(typeof(UpdateProfileRequest), nameof(ValidateDateOfBirth))]
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [RegularExpression(@"^\+?[0-9]{7,20}$", ErrorMessage = "Invalid phone number format.")]
        [DefaultValue("+84901234567")]
        public string? PhoneNumber { get; set; }

        [CustomValidation(typeof(UpdateProfileRequest), nameof(ValidateAvatarFile))]
        public IFormFile? AvatarFile { get; set; }

        // --- Custom validator for DateOfBirth ---
        public static ValidationResult? ValidateDateOfBirth(DateTime? dob, ValidationContext context)
        {
            if (dob == null) return ValidationResult.Success;

            if (dob > DateTime.UtcNow)
                return new ValidationResult("Date of birth cannot be in the future.");

            if (dob < DateTime.UtcNow.AddYears(-120))
                return new ValidationResult("Date of birth is too far in the past.");

            return ValidationResult.Success;
        }

        // --- Custom validator for AvatarFile ---
        public static ValidationResult? ValidateAvatarFile(IFormFile? file, ValidationContext context)
        {
            if (file == null) return ValidationResult.Success;

            const long maxFileSize = 5 * 1024 * 1024; // 5 MB
            if (file.Length > maxFileSize)
                return new ValidationResult("Avatar file size cannot exceed 10 MB.");

            return ValidationResult.Success;
        }
    }
}
