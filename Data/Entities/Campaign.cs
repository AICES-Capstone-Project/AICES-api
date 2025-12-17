using Data.Entities.Base;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("Campaigns")]
    public class Campaign : BaseEntity
    {
        [Key]
        public int CampaignId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        // Track who created the campaign
        [ForeignKey("Creator")]
        public int? CreatedBy { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public CampaignStatusEnum Status { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
        public User? Creator { get; set; }
        public ICollection<JobCampaign> JobCampaigns { get; set; } = new List<JobCampaign>();
        public ICollection<ResumeApplication> ResumeApplications { get; set; } = new List<ResumeApplication>();
        public ICollection<Comparison> Comparisons { get; set; } = new List<Comparison>();
    }
}
