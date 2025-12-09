using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IJobSkillService
    {
        Task<ServiceResponse> GetAllAsync();
        Task<ServiceResponse> GetByJobIdAndSkillIdAsync(int jobId, int skillId);
        Task<ServiceResponse> CreateAsync(JobSkillRequest request);
        Task<ServiceResponse> UpdateAsync(int jobId, int skillId, JobSkillRequest request);
        Task<ServiceResponse> DeleteAsync(int jobId, int skillId);
    }
}
