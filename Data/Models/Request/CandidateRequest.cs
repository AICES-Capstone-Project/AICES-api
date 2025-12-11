using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class CandidateCreateRequest
    {
        [Required, MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(255), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

    }

    public class CandidateUpdateRequest
    {
        [MaxLength(255)]
        public string? FullName { get; set; }

        [MaxLength(255), EmailAddress]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }
    }
}


