using System;
using Data.Enum;

namespace Data.Models.Response
{
    public class CandidateResponse
    {
        public int CandidateId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CandidateResumeResponse
    {
        public int ResumeId { get; set; }
        public int CompanyId { get; set; }
        public string? FileUrl { get; set; }
        public ResumeStatusEnum Status { get; set; }
        public string? FileName { get; set; }
        public bool? IsLatest { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    
    public class CandidateWithResumesResponse
    {
        public CandidateResponse Candidate { get; set; } = new CandidateResponse();
        public List<CandidateResumeResponse> Resumes { get; set; } = [];
    }
}


