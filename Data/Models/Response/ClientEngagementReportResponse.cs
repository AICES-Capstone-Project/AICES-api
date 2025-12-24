namespace Data.Models.Response
{
    /// <summary>
    /// Chỉ số Hoạt động của Khách hàng (Client Engagement)
    /// 2 chỉ số chính:
    /// 1. Tần suất sử dụng
    /// 2. Mức độ tin tưởng AI
    /// </summary>
    public class ClientEngagementReportResponse
    {
        /// <summary>
        /// 1. Tần suất sử dụng
        /// Số lượng Job/Campaign trung bình mỗi công ty tạo ra trong tháng
        /// </summary>
        public UsageFrequency UsageFrequency { get; set; } = new();

        /// <summary>
        /// 2. Mức độ tin tưởng AI
        /// Tỷ lệ ứng viên có điểm AI cao (>80) được HR chuyển sang mục Hiring Status
        /// </summary>
        public AiTrustLevel AiTrustLevel { get; set; } = new();
    }

    /// <summary>
    /// Tần suất sử dụng - Số lượng Job/Campaign trung bình mỗi công ty tạo ra
    /// </summary>
    public class UsageFrequency
    {
        /// <summary>
        /// Số lượng Job trung bình mỗi công ty tạo ra trong tháng
        /// </summary>
        public decimal AverageJobsPerCompanyPerMonth { get; set; }

        /// <summary>
        /// Số lượng Campaign trung bình mỗi công ty tạo ra trong tháng
        /// </summary>
        public decimal AverageCampaignsPerCompanyPerMonth { get; set; }
    }

    /// <summary>
    /// Mức độ tin tưởng AI
    /// Tỷ lệ ứng viên có điểm AI cao (>80) được HR chuyển sang mục Hiring Status
    /// </summary>
    public class AiTrustLevel
    {
        /// <summary>
        /// Tỷ lệ phần trăm ứng viên có điểm AI cao (>80) được HR chuyển sang Hiring Status
        /// </summary>
        public decimal TrustPercentage { get; set; }

        /// <summary>
        /// Tổng số ứng viên có điểm AI cao (>80)
        /// </summary>
        public int HighScoreCandidatesCount { get; set; }

        /// <summary>
        /// Số ứng viên có điểm AI cao (>80) được chuyển sang Hiring Status
        /// </summary>
        public int HighScoreCandidatesHiredCount { get; set; }
    }
}
