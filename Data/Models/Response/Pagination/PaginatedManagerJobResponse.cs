using System.Collections.Generic;

namespace Data.Models.Response.Pagination
{
    public class PaginatedManagerJobResponse : BasePaginatedResponse
    {
        public List<ManagerJobResponse> Jobs { get; set; } = new List<ManagerJobResponse>();
    }
}


