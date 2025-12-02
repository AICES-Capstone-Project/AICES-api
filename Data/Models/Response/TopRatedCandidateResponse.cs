using Data.Enum;

namespace Data.Models.Response
{
    public class TopRatedCandidateResponse
    {
        public string Name { get; set; } = string.Empty; // FullName từ ParsedCandidates
        public string JobTitle { get; set; } = string.Empty; // Title từ Job
        public decimal AIScore { get; set; } // TotalResumeScore từ AIScores
        public ResumeStatusEnum Status { get; set; } // ResumeStatus từ ParsedResumes
    }
}

