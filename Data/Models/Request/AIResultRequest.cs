using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Data.Models.Request
{
    public class AIResultRequest
    {
        [Required]
        [JsonPropertyName("queueJobId")]
        public string QueueJobId { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("resumeId")]
        public int ResumeId { get; set; }

        [JsonPropertyName("jobId")]
        public int? JobId { get; set; }

        [Required]
        [JsonPropertyName("totalResumeScore")]
        public decimal TotalResumeScore { get; set; }

        [JsonPropertyName("AIExplanation")]
        public object? AIExplanation { get; set; }

        [Required]
        [JsonPropertyName("AIScoreDetail")]
        public List<AIScoreDetailRequest> AIScoreDetail { get; set; } = new();

        [JsonPropertyName("rawJson")]
        public object? RawJson { get; set; }
    }

    public class AIScoreDetailRequest
    {
        [Required]
        public int CriteriaId { get; set; }

        public decimal Matched { get; set; } // Percentage of resume match with this criterion (0-100)

        [Required]
        public decimal Score { get; set; }

        public string? AINote { get; set; }
    }
}

