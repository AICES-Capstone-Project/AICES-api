using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response.Pagination
{
    public class PaginatedUserResponse : BasePaginatedResponse
    {
        public List<UserResponse> Users { get; set; } = new List<UserResponse>();
    }
}
