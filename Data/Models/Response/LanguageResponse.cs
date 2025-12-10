using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class LanguageResponse
    {
        public int LanguageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }
}

