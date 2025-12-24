namespace Data.Models.Response
{
    /// <summary>
    /// Báo cáo Hành vi Khách hàng (SaaS Admin Metrics)
    /// 3 chỉ số chính:
    /// 1. Active Company Tracking - Top users
    /// 2. Feature Adoption - Tính năng được sử dụng nhiều nhất
    /// 3. Churn Risk - Công ty có nguy cơ rời bỏ
    /// </summary>
    public class SaasAdminMetricsReportResponse
    {
        /// <summary>
        /// 1. Active Company Tracking - Những công ty đang sử dụng nhiều tài nguyên nhất
        /// </summary>
        public List<TopCompanyUsage> TopCompanies { get; set; } = new();

        /// <summary>
        /// 2. Feature Adoption - Tính năng nào được sử dụng nhiều nhất
        /// </summary>
        public FeatureAdoption FeatureAdoption { get; set; } = new();

        /// <summary>
        /// 3. Churn Risk - Công ty có nguy cơ rời bỏ nền tảng
        /// </summary>
        public List<ChurnRiskCompany> ChurnRiskCompanies { get; set; } = new();
    }

    /// <summary>
    /// Top Company Usage - Công ty sử dụng nhiều tài nguyên
    /// </summary>
    public class TopCompanyUsage
    {
        /// <summary>
        /// ID công ty
        /// </summary>
        public int CompanyId { get; set; }

        /// <summary>
        /// Tên công ty
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng CV đã upload
        /// </summary>
        public int TotalResumesUploaded { get; set; }

        /// <summary>
        /// Số lượng Job đã tạo
        /// </summary>
        public int TotalJobsCreated { get; set; }

        /// <summary>
        /// Số lượng Campaign đã tạo
        /// </summary>
        public int TotalCampaignsCreated { get; set; }

        /// <summary>
        /// Tổng điểm hoạt động (dựa trên tổng hợp các chỉ số)
        /// </summary>
        public int ActivityScore { get; set; }
    }

    /// <summary>
    /// Feature Adoption - Mức độ sử dụng các tính năng
    /// </summary>
    public class FeatureAdoption
    {
        /// <summary>
        /// Số lần sử dụng tính năng Screening (AI chấm điểm CV)
        /// </summary>
        public int ScreeningUsageCount { get; set; }

        /// <summary>
        /// Số lần sử dụng tính năng Tracking (Theo dõi ứng viên)
        /// </summary>
        public int TrackingUsageCount { get; set; }

        /// <summary>
        /// Số lần sử dụng tính năng Export (Xuất báo cáo)
        /// </summary>
        public int ExportUsageCount { get; set; }
    }

    /// <summary>
    /// Churn Risk Company - Công ty có nguy cơ rời bỏ nền tảng
    /// </summary>
    public class ChurnRiskCompany
    {
        /// <summary>
        /// ID công ty
        /// </summary>
        public int CompanyId { get; set; }

        /// <summary>
        /// Tên công ty
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Loại subscription hiện tại
        /// </summary>
        public string SubscriptionPlan { get; set; } = string.Empty;

        /// <summary>
        /// Mức độ rủi ro (Low, Medium, High)
        /// </summary>
        public string RiskLevel { get; set; } = string.Empty;
    }
}
