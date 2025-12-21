using System.Collections.Generic;

namespace Data.Models.Response
{
    public class CompanyStatsOverviewResponse
    {
        public int TotalJobs { get; set; }
        public List<Top5JobInCampaignResponse> Top5JobsInCampaigns { get; set; } = new();
        public List<Top5CampaignWithMostJobsResponse> Top5CampaignsWithMostJobs { get; set; } = new();
        public List<Top5CandidateWithMostJobsResponse> Top5CandidatesWithMostJobs { get; set; } = new();
        public List<Top5HighestScoreCVResponse> Top5HighestScoreCVs { get; set; } = new();
        public int OnTimeCampaignsThisMonth { get; set; }
    }

    public class Top5JobInCampaignResponse
    {
        public int JobId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CampaignCount { get; set; }
    }

    public class Top5CampaignWithMostJobsResponse
    {
        public int CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int JobCount { get; set; }
    }

    public class Top5CandidateWithMostJobsResponse
    {
        public int CandidateId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int JobCount { get; set; }
    }

    public class Top5HighestScoreCVResponse
    {
        public int ApplicationId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }
}

