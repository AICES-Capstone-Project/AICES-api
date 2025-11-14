using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class AIResultRequest
    {
        [Required]
        public string QueueJobId { get; set; } = string.Empty;

        [Required]
        public int ResumeId { get; set; }

        [Required]
        public decimal TotalResumeScore { get; set; }

        public string? AIExplanation { get; set; }

        [Required]
        public List<AIScoreDetailRequest> AIScoreDetail { get; set; } = new();

        public object? RawJson { get; set; }
    }

    public class AIScoreDetailRequest
    {
        [Required]
        public int CriteriaId { get; set; }

        public bool Matched { get; set; }

        [Required]
        public decimal Score { get; set; }

        public string? AINote { get; set; }
    }
}

