using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Data.Models.Request
{
    public class ComparisonAIResultRequest
    {
        [Required]
        [JsonPropertyName("queueJobId")]
        public string QueueJobId { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("comparisonId")]
        public int ComparisonId { get; set; }

        [JsonPropertyName("campaignId")]
        public int? CampaignId { get; set; }

        [JsonPropertyName("jobId")]
        public int? JobId { get; set; }

        [JsonPropertyName("companyId")]
        public int? CompanyId { get; set; }

        [JsonPropertyName("resultJson")]
        public object? ResultJson { get; set; } // Full comparison result from AI

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
