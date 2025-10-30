using Data.Enum;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class UpdateCompanyStatusRequest
    {
        [Required(ErrorMessage = "Status is required")]
        public CompanyStatusEnum Status { get; set; }
        public string? RejectionReason { get; set; }
    }
}
