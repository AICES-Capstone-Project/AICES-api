using Data.Enum;

namespace Data.Models.Response
{
    public class JobResumeDetailResponse
    {
        public int? ResumeId { get; set; }
        public int? ApplicationId { get; set; }
        public string QueueJobId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public ResumeStatusEnum Status { get; set; }
        public ApplicationStatusEnum ApplicationStatus { get; set; } = ApplicationStatusEnum.Pending;
        public int? CampaignId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public int CandidateId { get; set; }
        public string FullName { get; set; } = "Unknown";
        public string Email { get; set; } = "N/A";
        public string? PhoneNumber { get; set; }
        public string? MatchSkills { get; set; }
        public string? MissingSkills { get; set; }

        public decimal? TotalScore { get; set; }
        public decimal? AdjustedScore { get; set; }
        public string? AIExplanation { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ResumeScoreDetailResponse> ScoreDetails { get; set; } = new();
    }

    public class ResumeScoreDetailResponse
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public decimal Matched { get; set; }  // Percentage match (0-100)
        public decimal Score { get; set; }
        public string? AINote { get; set; }
    }
}

