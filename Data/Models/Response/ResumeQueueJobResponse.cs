namespace Data.Models.Response
{
    /// <summary>
    /// Response model for Redis queue job data when uploading resume
    /// </summary>
    public class ResumeQueueJobResponse
    {
        public int resumeId { get; set; }
        public string queueJobId { get; set; } = string.Empty;
        public int jobId { get; set; }
        public string fileUrl { get; set; } = string.Empty;
        public string? requirements { get; set; }
        public List<CriteriaQueueResponse> criteria { get; set; } = new();
    }

    /// <summary>
    /// Criteria information for queue job
    /// </summary>
    public class CriteriaQueueResponse
    {
        public int CriteriaId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }
}

