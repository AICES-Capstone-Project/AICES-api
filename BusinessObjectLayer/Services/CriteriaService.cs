using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CriteriaService : ICriteriaService
    {
        private readonly IUnitOfWork _uow;

        public CriteriaService(IUnitOfWork uow)
        {
            _uow = uow;
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

            var criteriaRepo = _uow.GetRepository<ICriteriaRepository>();
            await criteriaRepo.AddCriteriaAsync(criteria);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Criteria created successfully."
            };
        }

        public async Task<ServiceResponse> ReplaceCriteriaForJobAsync(int jobId, List<CriteriaRequest> criteriaRequests)
        {
            var validation = await CreateCriteriaForJobAsyncValidateOnly(criteriaRequests);
            if (validation.Status != SRStatus.Success)
            {
                return validation;
            }

            var criteriaRepo = _uow.GetRepository<ICriteriaRepository>();
            await criteriaRepo.DeleteByJobIdAsync(jobId);

            var criteria = criteriaRequests.Select(c => new Criteria
            {
                JobId = jobId,
                Name = c.Name,
                Weight = c.Weight
            }).ToList();

            await criteriaRepo.AddCriteriaAsync(criteria);
            await _uow.SaveChangesAsync();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Criteria updated successfully."
            };
        }

        private Task<ServiceResponse> CreateCriteriaForJobAsyncValidateOnly(List<CriteriaRequest> criteriaRequests)
        {
            if (criteriaRequests == null || criteriaRequests.Count < 2)
            {
                return Task.FromResult(new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "At least 2 criteria are required."
                });
            }

            if (criteriaRequests.Count >= 20)
            {
                return Task.FromResult(new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = "Maximum of 19 criteria can be provided."
                });
            }

            var totalWeight = criteriaRequests.Sum(c => c.Weight);
            if (Math.Abs(totalWeight - 1.0m) > 0.001m)
            {
                return Task.FromResult(new ServiceResponse
                {
                    Status = SRStatus.Validation,
                    Message = $"Total weight of all criteria must equal 1.0. Current total: {totalWeight}"
                });
            }

            return Task.FromResult(new ServiceResponse { Status = SRStatus.Success });
        }
    }
}
