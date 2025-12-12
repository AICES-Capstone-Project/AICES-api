using System.Collections.Generic;

namespace Data.Models.Response
{
    public class CandidateWithResumesResponse
    {
        public CandidateResponse Candidate { get; set; } = new CandidateResponse();
        public List<CandidateResumeResponse> Resumes { get; set; } = new List<CandidateResumeResponse>();
    }
}
