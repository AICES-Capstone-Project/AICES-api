using System.Collections.Generic;

namespace Data.Models.Response.Pagination
{
    public class PaginatedSelfJobResponse : BasePaginatedResponse
    {
        public List<SelfJobResponse> Jobs { get; set; } = new List<SelfJobResponse>();
    }
}

