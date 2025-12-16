using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Common;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class BannerConfigService : IBannerConfigService
    {
        private readonly IUnitOfWork _uow;
        private readonly CloudinaryHelper _cloudinaryHelper;

        public BannerConfigService(
            IUnitOfWork uow,
            CloudinaryHelper cloudinaryHelper)
        {
            _uow = uow;
            _cloudinaryHelper = cloudinaryHelper;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var bannerConfigRepo = _uow.GetRepository<IBannerConfigRepository>();
            var bannerConfigs = await bannerConfigRepo.GetBannersAsync(page, pageSize, search);
            var total = await bannerConfigRepo.GetTotalBannersAsync(search);

            var bannerConfigResponses = bannerConfigs.Select(b => new BannerConfigResponse
            {
                BannerId = b.BannerId,
                Title = b.Title,
                ColorCode = b.ColorCode,
                Source = b.Source,
                CreatedAt = b.CreatedAt
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Banner configs retrieved successfully.",
                Data = new PaginatedBannerConfigResponse
                {
                    BannerConfigs = bannerConfigResponses,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total
                }
            };
        }


        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var bannerConfigRepo = _uow.GetRepository<IBannerConfigRepository>();
            var bannerConfig = await bannerConfigRepo.GetByIdAsync(id);
            if (bannerConfig == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Banner config not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Data = new BannerConfigResponse
                {
                    BannerId = bannerConfig.BannerId,
                    Title = bannerConfig.Title,
                    ColorCode = bannerConfig.ColorCode,
                    Source = bannerConfig.Source,
                    CreatedAt = bannerConfig.CreatedAt
                }
            };
        }

        public async Task<ServiceResponse> CreateAsync(BannerConfigRequest request)
        {
            string? imageUrl = null;

            // Upload image to Cloudinary if provided
            if (request.Source != null)
            {
                var uploadResult = await _cloudinaryHelper.UploadImageAsync(
                    request.Source, 
                    "banners", 
                    1200,  // Banner width
                    400    // Banner height
                );

                if (!uploadResult.Success)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = $"Image upload failed: {uploadResult.ErrorMessage}"
                    };
                }

                imageUrl = uploadResult.Url;
            }

            await _uow.BeginTransactionAsync();
            try
            {
                var bannerConfig = new BannerConfig
                {
                    Title = request.Title,
                    ColorCode = request.ColorCode,
                    Source = imageUrl
                };

                var bannerConfigRepo = _uow.GetRepository<IBannerConfigRepository>();
                await bannerConfigRepo.AddAsync(bannerConfig);
                await _uow.SaveChangesAsync();
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Banner config created successfully.",
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResponse> UpdateAsync(int id, BannerConfigRequest request)
        {
            var bannerConfigRepo = _uow.GetRepository<IBannerConfigRepository>();
            var bannerConfig = await bannerConfigRepo.GetForUpdateAsync(id);
            if (bannerConfig == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Banner config not found."
                };
            }

            // Upload new image to Cloudinary if provided
            if (request.Source != null)
            {
                var uploadResult = await _cloudinaryHelper.UploadImageAsync(
                    request.Source, 
                    "banners", 
                    1200,  // Banner width
                    400    // Banner height
                );

                if (!uploadResult.Success)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = $"Image upload failed: {uploadResult.ErrorMessage}"
                    };
                }

                bannerConfig.Source = uploadResult.Url;
            }
            
            if (!string.IsNullOrEmpty(request.Title))
                bannerConfig.Title = request.Title;
            
            if (request.ColorCode != null)
                bannerConfig.ColorCode = request.ColorCode;

            await _uow.BeginTransactionAsync();
            try
            {
                await bannerConfigRepo.UpdateAsync(bannerConfig);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Banner config updated successfully.",
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
            var bannerConfigRepo = _uow.GetRepository<IBannerConfigRepository>();
            var bannerConfig = await bannerConfigRepo.GetForUpdateAsync(id);
            if (bannerConfig == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Banner config not found."
                };
            }

            await _uow.BeginTransactionAsync();
            try
            {
                bannerConfig.IsActive = false;
                await bannerConfigRepo.UpdateAsync(bannerConfig);
                await _uow.CommitTransactionAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Banner config deactivated successfully."
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

