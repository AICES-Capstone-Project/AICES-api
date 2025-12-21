using Data.Entities;
using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICampaignService
    {
        Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<ServiceResponse> GetByIdAsync(int id);
        Task<ServiceResponse> GetMyCampaignsAsync(int page = 1, int pageSize = 10, string? search = null, Data.Enum.CampaignStatusEnum? status = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<ServiceResponse> GetMyCampaignsByIdAsync(int id);
        Task<ServiceResponse> GetCampaignJobsAsync(int campaignId, int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> CreateAsync(CreateCampaignRequest request);
        Task<ServiceResponse> UpdateAsync(int id, UpdateCampaignRequest request);
        Task<ServiceResponse> AddJobsToCampaignAsync(int campaignId, AddJobsToCampaignRequest request);
        Task<ServiceResponse> RemoveJobsFromCampaignAsync(int campaignId, RemoveJobsFromCampaignRequest request);
        Task<ServiceResponse> SoftDeleteAsync(int id);
        Task<ServiceResponse> UpdateStatusAsync(int id, UpdateCampaignStatusRequest? request = null);
    }
}
