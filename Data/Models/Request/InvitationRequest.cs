using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class SendInvitationRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;
    }
}

