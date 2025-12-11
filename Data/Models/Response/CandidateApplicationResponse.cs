using Data.Enum;
using System;
using System.Collections.Generic;

namespace Data.Models.Response
{
    public class CandidateApplicationResponse
    {
        public int ApplicationId { get; set; }
        public int ResumeId { get; set; }
        public int? CandidateId { get; set; }
        public int JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int? CampaignId { get; set; }
        public string? CampaignTitle { get; set; }
        public ApplicationStatusEnum Status { get; set; }
        public decimal? TotalScore { get; set; }
        public decimal? AdjustedScore { get; set; }
        public string? MatchSkills { get; set; }
        public string? MissingSkills { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CandidateApplicationDetailResponse : CandidateApplicationResponse
    {
        public string? RequiredSkills { get; set; }
        public string? AIExplanation { get; set; }
        public List<ResumeScoreDetailResponse> ScoreDetails { get; set; } = new();
    }
}


