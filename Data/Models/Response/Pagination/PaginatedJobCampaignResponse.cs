using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedJobCampaignResponse : BasePaginatedResponse
    {
        public List<JobCampaignInfoResponse> Jobs { get; set; } = new List<JobCampaignInfoResponse>();
    }
}

