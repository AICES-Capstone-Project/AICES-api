using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedLevelResponse : BasePaginatedResponse
    {
        public List<LevelResponse> Levels { get; set; } = new List<LevelResponse>();
    }
}
