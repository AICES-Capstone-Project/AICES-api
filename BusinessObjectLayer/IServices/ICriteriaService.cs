using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICriteriaService
    {
        Task<ServiceResponse> CreateCriteriaForJobAsync(int jobId, List<CriteriaRequest> criteriaRequests);
    }
}
