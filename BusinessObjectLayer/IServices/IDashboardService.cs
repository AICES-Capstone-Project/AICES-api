using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IDashboardService
    {
        Task<ServiceResponse> GetTopCategorySpecByResumeCountAsync(int top = 10);
        Task<ServiceResponse> GetDashboardSummaryAsync();
        Task<ServiceResponse> GetTopRatedCandidatesAsync(int limit = 5);
        Task<ServiceResponse> GetSystemOverviewAsync();
        Task<ServiceResponse> GetSystemCompanyStatsAsync();
    }
}

