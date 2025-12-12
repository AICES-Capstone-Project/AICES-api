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

        [JsonPropertyName("applicationId")]
        public int? ApplicationId { get; set; }

        [JsonPropertyName("jobId")]
        public int? JobId { get; set; }

        [JsonPropertyName("campaignId")]
        public int? CampaignId { get; set; }

        [JsonPropertyName("totalResumeScore")]
        public decimal? TotalResumeScore { get; set; }

        [JsonPropertyName("AIExplanation")]
        public object? AIExplanation { get; set; }

        [JsonPropertyName("AIScoreDetail")]
        public List<ScoreDetailRequest> ScoreDetails { get; set; } = new();

        [JsonPropertyName("rawJson")]
        public object? RawJson { get; set; }
        [JsonPropertyName("requireSkills")]
        public string? RequiredSkills { get; set; }

        [JsonPropertyName("candidateInfo")]
        public CandidateInfoRequest? CandidateInfo { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public class ScoreDetailRequest
    {
        [Required]
        [JsonPropertyName("criteriaId")]
        public int CriteriaId { get; set; }

        [JsonPropertyName("matched")]
        public decimal Matched { get; set; } // Match percentage (0.0-1.0 from AI, stored as 0-100)

        [JsonPropertyName("rawScore")]
        public decimal? RawScore { get; set; } // Raw score from AI (0.0-100.0), optional

        [Required]
        [JsonPropertyName("score")]
        public decimal Score { get; set; } // Weighted score (rawScore * weight)

        [JsonPropertyName("AINote")]
        public string? AINote { get; set; }
    }

    public class CandidateInfoRequest
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonPropertyName("matchSkills")]
        public string? MatchSkills { get; set; }

        [JsonPropertyName("missingSkills")]
        public string? MissingSkills { get; set; }
    }
}

