using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedJobResponse : BasePaginatedResponse
    {
        public List<JobResponse> Jobs { get; set; } = new List<JobResponse>();
    }
}

