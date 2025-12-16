using System;
using System.Collections.Generic;

namespace Data.Models.Response.Pagination
{
    public class PaginatedEmploymentTypeResponse : BasePaginatedResponse
    {
        public List<EmploymentTypeResponse> EmploymentTypes { get; set; } = new List<EmploymentTypeResponse>();
    }
}


