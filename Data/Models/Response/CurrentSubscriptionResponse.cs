using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class CurrentSubscriptionResponse
    {
        public string SubscriptionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Price { get; set; }
        public int DurationDays { get; set; }
        public int ResumeLimit { get; set; }
        public int HoursLimit { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatusEnum SubscriptionStatus { get; set; }
    }
}

