using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedCompanyResponse : BasePaginatedResponse
    {
        public List<CompanyResponse> Companies { get; set; } = new List<CompanyResponse>();
    }
}

