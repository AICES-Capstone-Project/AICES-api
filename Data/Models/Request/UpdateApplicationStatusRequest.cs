using Data.Enum;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class UpdateApplicationStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        public ApplicationStatusEnum Status { get; set; }
        
        public string? Note { get; set; }
    }
}

