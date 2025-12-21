namespace Data.Models.Response
{
    public class CompanyUsageResponse
    {
        public int RegisteredOnly { get; set; }
        public int ActiveCompanies { get; set; }
        public int FrequentCompanies { get; set; }
        public CompanyUsageKpis Kpis { get; set; } = new();
    }

    public class CompanyUsageKpis
    {
        public decimal ActiveRate { get; set; }
        public decimal AiUsageRate { get; set; }
        public decimal ReturningRate { get; set; }
    }
}
