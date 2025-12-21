namespace Data.Models.Response
{
    public class JobStatisticsResponse
    {
        public int TotalJobs { get; set; }
        public int ActiveJobs { get; set; }
        public int DraftJobs { get; set; }
        public int ClosedJobs { get; set; }
        public int NewJobsThisMonth { get; set; }
        public decimal AverageApplicationsPerJob { get; set; }
        public JobsByStatusBreakdown StatusBreakdown { get; set; } = new();
        public List<TopCategoryJob> TopCategories { get; set; } = new();
    }

    public class JobsByStatusBreakdown
    {
        public int Published { get; set; }
        public int Draft { get; set; }
        public int Closed { get; set; }
    }

    public class TopCategoryJob
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int JobCount { get; set; }
    }
}
