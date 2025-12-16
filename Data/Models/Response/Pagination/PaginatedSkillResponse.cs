using System;
using System.Collections.Generic;

namespace Data.Models.Response.Pagination
{
    public class PaginatedSkillResponse : BasePaginatedResponse
    {
        public List<SkillResponse> Skills { get; set; } = new List<SkillResponse>();
    }
}


