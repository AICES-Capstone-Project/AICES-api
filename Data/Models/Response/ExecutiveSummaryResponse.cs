namespace Data.Models.Response
{
    public class ExecutiveSummaryResponse
    {
        public int TotalCompanies { get; set; }
        public int TotalJobs { get; set; }
        public int AiProcessedResumes { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
