using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class SubscriptionResponse
    {
        public int SubscriptionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Price { get; set; }
        public int DurationDays { get; set; }
        public int ResumeLimit { get; set; }
        public int HoursLimit { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
