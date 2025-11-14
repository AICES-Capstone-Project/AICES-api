using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class CompanySubscriptionResponse
    {
        public int ComSubId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int SubscriptionId { get; set; }
        public string SubscriptionName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatusEnum SubscriptionStatus { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

