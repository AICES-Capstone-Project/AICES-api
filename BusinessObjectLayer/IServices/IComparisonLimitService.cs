using Data.Models.Response;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IComparisonLimitService
    {
        Task<ServiceResponse> CheckComparisonLimitAsync(int companyId);
        Task<ServiceResponse> CheckComparisonLimitInTransactionAsync(int companyId);
    }
}

