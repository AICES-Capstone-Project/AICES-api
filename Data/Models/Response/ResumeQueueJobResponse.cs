namespace Data.Models.Response
{
    /// <summary>
    /// Response model for Redis queue job data when uploading resume
    /// </summary>
    public class ResumeQueueJobResponse
    {
        public int companyId { get; set; }
        public int resumeId { get; set; }
        public int applicationId { get; set; }
        public string queueJobId { get; set; } = string.Empty;
        public int campaignId { get; set; }
        public int jobId { get; set; }
        public string? jobTitle { get; set; }
        public string fileUrl { get; set; } = string.Empty;
        public string? requirements { get; set; }
        public string? skills { get; set; }
        public string? level { get; set; }
        public string? languages { get; set; }
        public string? category { get; set; }
        public string? specialization { get; set; }
        public string? employmentType { get; set; }
        public List<CriteriaQueueResponse> criteria { get; set; } = new();
        
        /// <summary>
        /// Processing mode: "parse" for first upload, "rescore" for re-analysis
        /// </summary>
        public string mode { get; set; } = "parse";
        
        /// <summary>
        /// Parsed resume data (JSON) - only used when mode = "score"
        /// </summary>
        public object? parsedData { get; set; }
    }

    /// <summary>
    /// Criteria information for queue job
    /// </summary>
    public class CriteriaQueueResponse
    {
        public int criteriaId { get; set; }
        public string name { get; set; } = string.Empty;
        public decimal weight { get; set; }
    }
}

