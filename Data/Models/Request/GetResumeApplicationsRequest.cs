using Data.Enum;

namespace Data.Models.Request
{
    public class GetResumeApplicationsRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public decimal? MinScore { get; set; }
        public decimal? MaxScore { get; set; }
        public ApplicationStatusEnum? ApplicationStatus { get; set; }
        public ResumeSortByEnum SortBy { get; set; } = ResumeSortByEnum.HighestScore;
        public ProcessingModeEnum? ProcessingMode { get; set; }
    }
}

