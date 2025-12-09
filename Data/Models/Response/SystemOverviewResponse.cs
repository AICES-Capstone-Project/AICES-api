namespace Data.Models.Response
{
    public class SystemOverviewResponse
    {
        public int TotalCompanies { get; set; }
        public int TotalUsers { get; set; }
        public int TotalJobs { get; set; }
        public int TotalResumes { get; set; }
        public int TotalCompanySubscriptions { get; set; }
        public int TotalSubscriptions { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}

