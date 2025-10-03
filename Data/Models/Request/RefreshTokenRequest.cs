using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class RefreshTokenRequest
    {
        [Required]
        [DefaultValue("34grtfh-22h3-g42-bsf0-1sgrgs0971cf")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
