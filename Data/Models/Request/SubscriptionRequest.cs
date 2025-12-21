using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class SubscriptionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Price { get; set; }
        public DurationEnum Duration { get; set; }
        public int ResumeLimit { get; set; }
        public int HoursLimit { get; set; }
        public int? CompareLimit { get; set; }
        public int? CompareHoursLimit { get; set; }
        public string? StripePriceId { get; set; }
    }
}
