using Data.Enum;

namespace Data.Models.Response
{
    public class JobResumeListResponse
    {
        public int ResumeId { get; set; }
        public ResumeStatusEnum Status { get; set; }

        public string FullName { get; set; } = "Unknown";

        public decimal? TotalScore { get; set; }
        public decimal? AdjustedScore { get; set; }
    }
}

