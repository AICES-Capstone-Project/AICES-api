using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedBannerConfigResponse : BasePaginatedResponse
    {
        public List<BannerConfigResponse> BannerConfigs { get; set; } = new List<BannerConfigResponse>();
    }
}

