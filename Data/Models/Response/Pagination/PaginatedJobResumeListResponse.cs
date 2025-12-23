namespace Data.Models.Response.Pagination
{
    public class PaginatedJobResumeListResponse : BasePaginatedResponse
    {
        public List<JobResumeListResponse> Resumes { get; set; } = new List<JobResumeListResponse>();
    }
}
