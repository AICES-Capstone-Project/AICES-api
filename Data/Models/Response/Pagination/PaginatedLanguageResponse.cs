using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedLanguageResponse : BasePaginatedResponse
    {
        public List<LanguageResponse> Languages { get; set; } = new List<LanguageResponse>();
    }
}

