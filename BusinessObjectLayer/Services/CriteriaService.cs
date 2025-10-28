using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CriteriaService : ICriteriaService
    {
        private readonly ICriteriaRepository _criteriaRepository;

        public CriteriaService(ICriteriaRepository criteriaRepository)
        {
            _criteriaRepository = criteriaRepository;
        }

        public async Task<ServiceResponse> CreateCriteriaForJobAsync(int jobId, List<CriteriaRequest> criteriaRequests)
        {
            if (criteriaRequests == null || criteriaRequests.Count < 2)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "At least 2 criteria are required."
                };
            }

            if (criteriaRequests.Count >= 20)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Maximum of 19 criteria can be provided."
                };
            }

            var totalWeight = criteriaRequests.Sum(c => c.Weight);
            if (Math.Abs(totalWeight - 1.0m) > 0.001m)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = $"Total weight of all criteria must equal 1.0. Current total: {totalWeight}"
                };
            }

            var criteria = criteriaRequests.Select(c => new Criteria
            {
                JobId = jobId,
                Name = c.Name,
                Weight = c.Weight
            }).ToList();

            await _criteriaRepository.AddCriteriaAsync(criteria);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Criteria created successfully."
            };
        }
    }
}
