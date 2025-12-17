using Data.Models.Request;
using Data.Models.Response;

namespace BusinessObjectLayer.IServices
{
    public interface IComparisonService
    {
        Task<ServiceResponse> CompareApplicationsAsync(CompareApplicationsRequest request);
        Task<ServiceResponse> ProcessComparisonAIResultAsync(ComparisonAIResultRequest request);
        Task<ServiceResponse> GetComparisonResultAsync(int comparisonId);
        Task<ServiceResponse> GetComparisonsByJobAndCampaignAsync(int jobId, int campaignId);
    }
}

