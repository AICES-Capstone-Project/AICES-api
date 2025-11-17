using Data.Enum;

namespace Data.Models.Response
{
    /// <summary>
    /// Response model for GET /company/self/jobs/{jobId}/resumes
    /// List of resumes for a specific job
    /// </summary>
    public class JobResumeListResponse
    {
        // From ParsedResume
        public int ResumeId { get; set; }
        public ResumeStatusEnum Status { get; set; }

        // From ParsedCandidates
        public string FullName { get; set; } = "Unknown";

        // From AIScores
        public decimal? TotalResumeScore { get; set; }
    }
}

