namespace Data.Models.Response
{
    public class DashboardSummaryResponse
    {
        public int ActiveJobs { get; set; } // Số Job đang mở (Published)
        public int TotalCampaigns { get; set; } // Tổng số campaign của công ty
        public int TotalPublicCampaigns { get; set; } // Tổng số campaign public (đang đăng tuyển)
        public int TotalMembers { get; set; } // Tổng member (nhân sự) trong công ty
        public int AiProcessed { get; set; } // Số CV đã được AI parse & score
        public int ResumeCreditsRemaining { get; set; } // Số lượt parse còn lại trong gói
        public int MaxResumeCredits { get; set; } // Số lượt parse tối đa trong gói
        public DateTime? ResumeTimeRemaining { get; set; } // Thời gian còn lại của gói hoặc reset
        public int ComparisonCreditsRemaining { get; set; } // Số lượt compare còn lại trong gói
        public int MaxComparisonCredits { get; set; } // Số lượt compare tối đa trong gói
        public DateTime? ComparisonTimeRemaining { get; set; } // Thời gian còn lại của gói hoặc reset
    }
}

