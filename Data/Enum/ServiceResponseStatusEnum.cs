using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum SRStatus
    {
        Success = 200,
        Error = 500,
        NotFound = 404,
        Duplicated = 400,
        Unauthorized = 401,
        Forbidden = 501
    }
}
