using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IReportService
    {
        Task<ServiceResponse> ExportJobCandidatesToExcelAsync(int campaignId, int jobId);
        Task<ServiceResponse> ExportJobCandidatesToPdfAsync(int campaignId, int jobId);
        Task<ServiceResponse> GetExecutiveSummaryAsync();
        Task<ServiceResponse> GetCompaniesOverviewAsync();
        Task<ServiceResponse> GetCompaniesUsageAsync();
        Task<ServiceResponse> GetJobsStatisticsAsync();
        Task<ServiceResponse> GetJobsEffectivenessAsync();
        Task<ServiceResponse> GetAiParsingQualityAsync();
        Task<ServiceResponse> GetAiScoringDistributionAsync();
        Task<ServiceResponse> GetSubscriptionRevenueAsync();

        /// <summary>
        /// Get AI System Health Report - Chỉ số Sức khỏe AI & Lỗi hệ thống
        /// Bao gồm: Tỷ lệ thành công, Tỷ lệ lỗi, Phân tích nguyên nhân lỗi, Thời gian xử lý trung bình
        /// </summary>
        Task<ServiceResponse> GetAiSystemHealthReportAsync();

        /// <summary>
        /// Get Client Engagement Report - Chỉ số Hoạt động của Khách hàng
        /// Bao gồm: Tần suất sử dụng, Mức độ tin tưởng AI
        /// </summary>
        Task<ServiceResponse> GetClientEngagementReportAsync();
        
            /// <summary>
            /// Get SaaS Admin Metrics Report - Báo cáo Hành vi Khách hàng
            /// Bao gồm: Top users, Feature adoption, Churn risk
            /// </summary>
            Task<ServiceResponse> GetSaasAdminMetricsReportAsync();
        
        // Export methods for system reports
        Task<ServiceResponse> ExportExecutiveSummaryToExcelAsync();
        Task<ServiceResponse> ExportExecutiveSummaryToPdfAsync();
        Task<ServiceResponse> ExportCompaniesOverviewToExcelAsync();
        Task<ServiceResponse> ExportCompaniesOverviewToPdfAsync();
        Task<ServiceResponse> ExportCompaniesUsageToExcelAsync();
        Task<ServiceResponse> ExportCompaniesUsageToPdfAsync();
        Task<ServiceResponse> ExportJobsStatisticsToExcelAsync();
        Task<ServiceResponse> ExportJobsStatisticsToPdfAsync();
        Task<ServiceResponse> ExportJobsEffectivenessToExcelAsync();
        Task<ServiceResponse> ExportJobsEffectivenessToPdfAsync();
        Task<ServiceResponse> ExportAiParsingQualityToExcelAsync();
        Task<ServiceResponse> ExportAiParsingQualityToPdfAsync();
        Task<ServiceResponse> ExportAiScoringDistributionToExcelAsync();
        Task<ServiceResponse> ExportAiScoringDistributionToPdfAsync();
        Task<ServiceResponse> ExportSubscriptionRevenueToExcelAsync();
        Task<ServiceResponse> ExportSubscriptionRevenueToPdfAsync();

        /// <summary>
        /// Export all system reports into a single Excel file with multiple sheets.
        /// </summary>
        Task<ServiceResponse> ExportAllSystemReportsToExcelAsync();

        /// <summary>
        /// Export all system reports into a single, formatted PDF document.
        /// </summary>
        Task<ServiceResponse> ExportAllSystemReportsToPdfAsync();
    }
}
