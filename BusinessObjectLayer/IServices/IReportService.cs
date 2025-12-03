using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IReportService
    {
        Task<ServiceResponse> ExportJobCandidatesToExcelAsync(int jobId);
    }
}
