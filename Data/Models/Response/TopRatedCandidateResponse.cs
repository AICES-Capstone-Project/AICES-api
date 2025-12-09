using Data.Enum;

namespace Data.Models.Response
{
    public class TopRatedCandidateResponse
    {
        public string Name { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public ResumeStatusEnum Status { get; set; }
    }
}

