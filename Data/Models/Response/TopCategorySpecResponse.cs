namespace Data.Models.Response
{
    public class TopCategorySpecResponse
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int SpecializationId { get; set; }
        public string SpecializationName { get; set; } = string.Empty;
        public int ResumeCount { get; set; }
    }
}

