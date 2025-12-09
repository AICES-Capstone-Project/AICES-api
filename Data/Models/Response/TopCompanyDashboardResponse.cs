namespace Data.Models.Response
{
    public class TopCompanyDashboardResponse
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int ResumeCount { get; set; }
        public int JobCount { get; set; }
    }
}

