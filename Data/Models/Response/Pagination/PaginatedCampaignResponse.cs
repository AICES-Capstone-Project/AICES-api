using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedCampaignResponse : BasePaginatedResponse
    {
        public List<CampaignResponse> Campaigns { get; set; } = new List<CampaignResponse>();
    }
}
