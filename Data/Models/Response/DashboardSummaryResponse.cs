namespace Data.Models.Response
{
    public class DashboardSummaryResponse
    {
        public int ActiveJobs { get; set; } // Số Job đang mở (Published)
        public int TotalMembers { get; set; } // Tổng member (nhân sự) trong công ty
        public int AiProcessed { get; set; } // Số CV đã được AI parse & score
        public int CreditsRemaining { get; set; } // Số lượt parse còn lại trong gói
    }
}

