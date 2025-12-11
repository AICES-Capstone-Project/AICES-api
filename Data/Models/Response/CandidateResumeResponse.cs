using Data.Enum;
using System;

namespace Data.Models.Response
{
    public class CandidateResumeResponse
    {
        public int ResumeId { get; set; }
        public int CompanyId { get; set; }
        public string? FileUrl { get; set; }
        public string? QueueJobId { get; set; }
        public ResumeStatusEnum Status { get; set; }
        public bool? IsLatest { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}


