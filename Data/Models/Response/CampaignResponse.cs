using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class CampaignResponse
    {
        public int CampaignId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public CampaignStatusEnum Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<int> JobIds { get; set; } = new List<int>();
    }
}

