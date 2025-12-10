using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class JobCampaignInfoResponse
    {
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int TargetQuantity { get; set; }
        public int CurrentHired { get; set; }
    }
}

