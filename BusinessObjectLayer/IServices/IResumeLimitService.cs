using Data.Models.Response;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IResumeLimitService
    {
        Task<ServiceResponse> CheckResumeLimitAsync(int companyId);
    }
}

