using Data.Enum;

namespace Data.Models.Response
{
    public class ResumeResultResponse
    {
        public ResumeStatusEnum Status { get; set; }
        public ResumeResultData? Data { get; set; }
    }

    public class ResumeResultData
    {
        public int ResumeId { get; set; }
        public decimal TotalScore { get; set; }
        public decimal? AdjustedScore { get; set; }
        public string? AIExplanation { get; set; }
        public List<ScoreDetailResponse> ScoreDetails { get; set; } = new();
    }

    public class ScoreDetailResponse
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public decimal Matched { get; set; } // Percentage of resume match with this criterion (0-100)
        public decimal Score { get; set; }
        public string? AINote { get; set; }
    }
}

