namespace Data.Models.Response
{
    public class AiScoringDistributionResponse
    {
        public decimal SuccessRate { get; set; }
        public ScoreDistribution ScoreDistribution { get; set; } = new();
        public decimal AverageProcessingTimeMs { get; set; }
        public List<string> CommonErrors { get; set; } = new();
        public ScoringStatistics Statistics { get; set; } = new();
    }

    public class ScoreDistribution
    {
        public decimal High { get; set; }  // Score > 75
        public decimal Medium { get; set; } // Score 50-75
        public decimal Low { get; set; }    // Score < 50
    }

    public class ScoringStatistics
    {
        public int TotalScored { get; set; }
        public decimal AverageScore { get; set; }
        public decimal MedianScore { get; set; }
    }
}
