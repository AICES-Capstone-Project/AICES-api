using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class CriteriaResponse
    {
        public int CriteriaId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }
}
