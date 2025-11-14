using Data.Enum;

namespace Data.Models.Response
{
    public class ResumeUploadResponse
    {
        public int ResumeId { get; set; }
        public string QueueJobId { get; set; } = string.Empty;
        public ResumeStatusEnum Status { get; set; }
    }
}

