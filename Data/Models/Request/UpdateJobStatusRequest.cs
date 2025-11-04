using Data.Enum;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class UpdateJobStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        public JobStatusEnum Status { get; set; }
    }
}


