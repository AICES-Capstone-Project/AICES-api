using Data.Enum;

namespace Data.Models.Response
{
    public class ResumeUploadResponse
    {
        public int ApplicationId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }
}

