using System;

namespace Data.Models.Response
{
    /// <summary>
    /// Response model for comparison list items (lightweight, without ResultData)
    /// </summary>
    public class ComparisonResponse
    {
        public int ComparisonId { get; set; }
        public int JobId { get; set; }
        public int? CampaignId { get; set; }
        public string? ComparisonName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool HasResult { get; set; } // Indicates if ResultData exists without loading it
    }

    /// <summary>
    /// Response model for comparison detail with full result data
    /// </summary>
    public class ComparisonDetailResponse
    {
        public int ComparisonId { get; set; }
        public int JobId { get; set; }
        public int? CampaignId { get; set; }
        public string? ComparisonName { get; set; }
        public string Status { get; set; } = string.Empty;
        public object? ResultData { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool HasResult { get; set; }
    }
}
