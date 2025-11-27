using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class PaymentDetailResponse
    {
        public int PaymentId { get; set; }
        public int CompanyId { get; set; }
        public int? ComSubId { get; set; }
        public PaymentStatusEnum PaymentStatus { get; set; }
        public string? InvoiceUrl { get; set; }
        public TransactionDetail? Transaction { get; set; }
        public CompanySubscriptionDetail? CompanySubscription { get; set; }
    }

    public class TransactionDetail
    {
        public int TransactionId { get; set; }
        public string? TransactionRef { get; set; }
        public TransactionGatewayEnum Gateway { get; set; }
        public int Amount { get; set; }
        public string? Currency { get; set; }
        public string? PayerName { get; set; }
        public string? BankCode { get; set; }
        public DateTime? TransactionTime { get; set; }
    }

    public class CompanySubscriptionDetail
    {
        public int SubscriptionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatusEnum Status { get; set; }
    }
}

