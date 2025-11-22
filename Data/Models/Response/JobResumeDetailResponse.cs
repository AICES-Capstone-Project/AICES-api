using Data.Enum;

namespace Data.Models.Response
{
    /// <summary>
    /// Response model for GET /company/self/jobs/{jobId}/resumes/{resumeId}
    /// Detailed information about a specific resume
    /// </summary>
    public class JobResumeDetailResponse
    {
        // From ParsedResume
        public int ResumeId { get; set; }
        public string QueueJobId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public ResumeStatusEnum Status { get; set; }
        public DateTime? CreatedAt { get; set; }

        // From ParsedCandidates
        public string FullName { get; set; } = "Unknown";
        public string Email { get; set; } = "N/A";
        public string? PhoneNumber { get; set; }

        // From AIScores
        public decimal? TotalResumeScore { get; set; }
        public string? AIExplanation { get; set; }

        // From AIScoreDetail (array)
        public List<ResumeScoreDetailResponse> ScoreDetails { get; set; } = new();
    }

    /// <summary>
    /// Detail of AI score for each criterion
    /// </summary>
    public class ResumeScoreDetailResponse
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public decimal Matched { get; set; }  // Percentage match (0-100)
        public decimal Score { get; set; }
        public string? AINote { get; set; }
    }
}

