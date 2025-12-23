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
    }
}
