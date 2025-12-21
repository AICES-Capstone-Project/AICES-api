namespace Data.Models.Response
{
    public class AiParsingQualityResponse
    {
        public decimal SuccessRate { get; set; }
        public int TotalResumes { get; set; }
        public int SuccessfulParsing { get; set; }
        public int FailedParsing { get; set; }
        public decimal AverageProcessingTimeMs { get; set; }
        public List<ErrorStatistic> CommonErrors { get; set; } = new();
    }

    public class ErrorStatistic
    {
        public string ErrorType { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}
