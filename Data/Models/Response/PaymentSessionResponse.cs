using Data.Enum;

namespace Data.Models.Response
{
    public class PaymentSessionResponse
    {
        public int PaymentId { get; set; }
        public int CompanyId { get; set; }
        public PaymentStatusEnum PaymentStatus { get; set; }
        public string? InvoiceUrl { get; set; }
        public string SessionStatus { get; set; } = string.Empty; // "complete", "expired", "open"
        public string? StripeSubscriptionId { get; set; }
        public int? ComSubId { get; set; }
        public string? SubscriptionName { get; set; }
    }
}

