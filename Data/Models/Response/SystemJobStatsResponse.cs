namespace Data.Models.Response
{
    public class SystemJobStatsResponse
    {
        public int TotalJobs { get; set; }
        public int ActiveJobs { get; set; }
        public int ClosedJobs { get; set; }
        public int ExpiredJobs { get; set; }
        public int NewJobsThisMonth { get; set; }
    }
}

