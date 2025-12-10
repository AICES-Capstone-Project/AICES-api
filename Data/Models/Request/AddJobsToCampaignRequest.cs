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
        [Required(ErrorMessage = "Jobs are required.")]
        [MinLength(1, ErrorMessage = "At least one job is required.")]
        public List<JobWithTargetRequest> Jobs { get; set; } = new List<JobWithTargetRequest>();
    }
}

