namespace Data.Models.Response
{
    public class SubscriptionRevenueResponse
    {
        public int FreeCompanies { get; set; }
        public int PaidCompanies { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public string PopularPlan { get; set; } = string.Empty;
        public SubscriptionBreakdown Breakdown { get; set; } = new();
    }

    public class SubscriptionBreakdown
    {
        public List<PlanStatistic> PlanStatistics { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerCompany { get; set; }
    }

    public class PlanStatistic
    {
        public int SubscriptionId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int CompanyCount { get; set; }
        public decimal Revenue { get; set; }
    }
}
