using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class AddJobsToCampaignRequest
    {
        [Required(ErrorMessage = "Job IDs are required.")]
        [MinLength(1, ErrorMessage = "At least one job ID is required.")]
        public List<int> JobIds { get; set; } = new List<int>();
    }
}
