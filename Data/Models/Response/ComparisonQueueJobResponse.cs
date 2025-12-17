using System.Collections.Generic;

namespace Data.Models.Response
{
    /// <summary>
    /// Response model for Redis queue job data when comparing candidates
    /// </summary>
    public class ComparisonQueueJobResponse
    {
        public int comparisonId { get; set; }
        public string queueJobId { get; set; } = string.Empty;
        public int companyId { get; set; }
        public int campaignId { get; set; }
        public int jobId { get; set; }
        public string? jobTitle { get; set; }
        public string? requirements { get; set; }
        public string? skills { get; set; }
        public string? level { get; set; }
        public string? languages { get; set; }
        public string? specialization { get; set; }
        public string? employmentType { get; set; }
        public List<CriteriaQueueResponse> criteria { get; set; } = new();
        public List<CandidateComparisonData> candidates { get; set; } = new();
    }

    /// <summary>
    /// Candidate data for comparison
    /// </summary>
    public class CandidateComparisonData
    {
        public int applicationId { get; set; }
        public object? parsedData { get; set; } // Resume parsed JSON
        public string? matchSkills { get; set; }
        public string? missingSkills { get; set; }
        public decimal? totalScore { get; set; }
    }
}
