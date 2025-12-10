using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class UpdateCampaignStatusRequest
    {
        [Required(ErrorMessage = "Status is required.")]
        public CampaignStatusEnum Status { get; set; }
    }
}

