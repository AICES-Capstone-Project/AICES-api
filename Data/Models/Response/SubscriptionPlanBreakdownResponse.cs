namespace Data.Models.Response
{
    public class SubscriptionPlanBreakdownResponse
    {
        public string PlanName { get; set; } = string.Empty;
        public int ActiveSubscriptions { get; set; }
        public decimal MonthlyRevenue { get; set; }
    }
}

