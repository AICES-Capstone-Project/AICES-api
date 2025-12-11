using System;

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
}


