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

        [JsonPropertyName("totalResumeScore")]
        public decimal? TotalResumeScore { get; set; }

        [JsonPropertyName("AIExplanation")]
        public object? AIExplanation { get; set; }

        [JsonPropertyName("scoreDetails")]
        public List<ScoreDetailRequest> ScoreDetails { get; set; } = new();

        [JsonPropertyName("rawJson")]
        public object? RawJson { get; set; }

        [JsonPropertyName("candidateInfo")]
        public CandidateInfoRequest? CandidateInfo { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class ScoreDetailRequest
    {
        [Required]
        public int CriteriaId { get; set; }

        public decimal Matched { get; set; } // Percentage of resume match with this criterion (0-100)

        [Required]
        public decimal Score { get; set; }

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

