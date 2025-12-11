namespace Data.Models.Response.Pagination
{
    public class PaginatedCandidateResponse : BasePaginatedResponse
    {
        public List<CandidateResponse> Candidates { get; set; } = new List<CandidateResponse>();
    }
}


