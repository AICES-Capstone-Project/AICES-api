namespace Data.Models.Response.Pagination
{
    public class PaginatedCandidateApplicationResponse : BasePaginatedResponse
    {
        public List<CandidateApplicationResponse> Applications { get; set; } = new List<CandidateApplicationResponse>();
    }
}

