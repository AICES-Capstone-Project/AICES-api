namespace Data.Models.Response
{
    public class SystemRevenueResponse
    {
        public string Month { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal FromNewSubscriptions { get; set; }
    }
}

