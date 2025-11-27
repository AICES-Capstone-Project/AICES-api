using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class TransactionResponse
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
}

