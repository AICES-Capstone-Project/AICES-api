using Data.Enum;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class UpdateUserStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        public UserStatusEnum Status { get; set; }
    }
}

