using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedCategoryResponse
    {
        public List<CategoryResponse> Categories { get; set; } = new();
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
