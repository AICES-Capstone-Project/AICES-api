using Data.Enum;

namespace Data.Models.Request
{
    public class GetJobResumesRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public decimal? MinScore { get; set; }
        public decimal? MaxScore { get; set; }
        public ApplicationStatusEnum? ApplicationStatus { get; set; }
    }
}
