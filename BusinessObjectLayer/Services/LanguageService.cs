using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class LanguageService : ILanguageService
    {
        private readonly IUnitOfWork _uow;

        public LanguageService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var languageRepo = _uow.GetRepository<ILanguageRepository>();
            var languages = await languageRepo.GetAllAsync(page, pageSize, search);
            var total = await languageRepo.GetTotalAsync(search);
            var result = languages.Select(l => new LanguageResponse
            {
                LanguageId = l.LanguageId,
                Name = l.Name,
                CreatedAt = l.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Languages retrieved successfully.",
                Data = new PaginatedLanguageResponse
                {
                    Languages = result,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize
                }
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var languageRepo = _uow.GetRepository<ILanguageRepository>();
            var language = await languageRepo.GetByIdAsync(id);
            if (language == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Language not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Language retrieved successfully.",
                Data = new LanguageResponse
                {
                    LanguageId = language.LanguageId,
                    Name = language.Name,
                    CreatedAt = language.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(LanguageRequest request)
        {
            var languageRepo = _uow.GetRepository<ILanguageRepository>();
            
            if (await languageRepo.ExistsByNameAsync(request.Name))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Language name already exists."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var language = new Language
                {
                    Name = request.Name
                };

                await languageRepo.AddAsync(language);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Language created successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, LanguageRequest request)
        {
            var languageRepo = _uow.GetRepository<ILanguageRepository>();
            var language = await languageRepo.GetForUpdateAsync(id);
            if (language == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Language not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                language.Name = request.Name ?? language.Name;
                languageRepo.Update(language);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Language updated successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var languageRepo = _uow.GetRepository<ILanguageRepository>();
            var language = await languageRepo.GetForUpdateAsync(id);
            if (language == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Language not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                language.IsActive = false;
                languageRepo.Update(language);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Language deleted successfully."
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }
    }
}

