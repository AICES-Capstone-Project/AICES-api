using System.Collections.Generic;

namespace Data.Models.Response
{
    public class UsageHistoryResponse
    {
        public string Range { get; set; } = string.Empty; // "week", "month", "year"
        public string Unit { get; set; } = string.Empty;  // "day", "month"
        public List<string> Labels { get; set; } = [];
        public List<int> ResumeUploads { get; set; } = [];
        public List<int> AiComparisons { get; set; } = [];
        public int ResumeLimit { get; set; }
        public int? AiComparisonLimit { get; set; }
    }
}

