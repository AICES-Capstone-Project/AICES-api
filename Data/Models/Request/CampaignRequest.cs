using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class CampaignRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateTime EndDate { get; set; }

        public CampaignStatusEnum Status { get; set; } = CampaignStatusEnum.Published;

        public List<JobWithTargetRequest>? Jobs { get; set; }
    }

    public class CreateCampaignRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateTime EndDate { get; set; }

        public List<JobWithTargetRequest>? Jobs { get; set; }
    }

    public class UpdateCampaignRequest
    {
        [MaxLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public CampaignStatusEnum? Status { get; set; }

        public List<JobWithTargetRequest>? Jobs { get; set; }
    }
}

