using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IReportService
    {
        Task<ServiceResponse> ExportJobCandidatesToExcelAsync(int campaignId, int jobId);
        Task<ServiceResponse> ExportJobCandidatesToPdfAsync(int campaignId, int jobId);
        Task<ServiceResponse> GetExecutiveSummaryAsync();
    }
}
