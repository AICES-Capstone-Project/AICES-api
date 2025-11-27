using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class PaymentListResponse
    {
        public int PaymentId { get; set; }
        public int CompanyId { get; set; }
        public int? ComSubId { get; set; }
        public PaymentStatusEnum PaymentStatus { get; set; }
        public SubscriptionStatusEnum? SubscriptionStatus { get; set; }
        public int Amount { get; set; }
        public string? Currency { get; set; }
        public string? SubscriptionName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? TransactionTime { get; set; }
    }
}

