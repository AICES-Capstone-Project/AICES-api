using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class BannerConfigResponse
    {
        public int BannerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public string? Source { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}


