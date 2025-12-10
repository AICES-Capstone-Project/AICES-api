using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("JobCampaigns")]
    public class JobCampaign
    {
        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Campaign")]
        public int CampaignId { get; set; }

        public int TargetQuantity { get; set; }

        public int CurrentHired { get; set; }

        // Navigation
        public Job Job { get; set; } = null!;
        public Campaign Campaign { get; set; } = null!;
    }
}
