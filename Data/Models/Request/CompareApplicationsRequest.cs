using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class CompareApplicationsRequest
    {
        [Required]
        public int JobId { get; set; }

        [Required]
        public int CampaignId { get; set; }

        [Required]
        [MinLength(2, ErrorMessage = "At least 2 applications are required for comparison")]
        [MaxLength(5, ErrorMessage = "Maximum 5 applications can be compared at once")]
        public List<int> ApplicationIds { get; set; } = new();
    }
}
