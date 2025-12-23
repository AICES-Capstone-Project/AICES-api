using Data.Enum;

namespace Data.Models.Response
{
    public class JobResumeListResponse
    {
        public int ResumeId { get; set; }
        public int ApplicationId { get; set; }
        public ResumeStatusEnum? ResumeStatus { get; set; }
        public ApplicationStatusEnum? ApplicationStatus { get; set; }
        public ApplicationErrorEnum? ApplicationErrorType { get; set; }

        public string FullName { get; set; } = "Unknown";

        public decimal? TotalScore { get; set; }
        public decimal? AdjustedScore { get; set; }
        public string? Note { get; set; }
    }
}

