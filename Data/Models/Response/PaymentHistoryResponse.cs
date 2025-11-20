using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class PaymentHistoryResponse
    {
        public int PaymentId { get; set; }
        public PaymentStatusEnum Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }

        public string SubscriptionName { get; set; } = string.Empty;
        public int DurationDays { get; set; }

        public List<TransactionItem> Transactions { get; set; } = new();
    }

    public class TransactionItem
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public TransactionGatewayEnum Gateway { get; set; }
        public string? ResponseCode { get; set; }
        public string? ResponseMessage { get; set; }
        public DateTime? TransactionTime { get; set; }
    }
}
