using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Common;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using Data.Models.Response.Pagination;
using DataAccessLayer.IRepositories;
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
        private readonly IBannerConfigRepository _bannerConfigRepository;
        private readonly CloudinaryHelper _cloudinaryHelper;

        public BannerConfigService(
            IBannerConfigRepository bannerConfigRepository,
            CloudinaryHelper cloudinaryHelper)
        {
            _bannerConfigRepository = bannerConfigRepository;
            _cloudinaryHelper = cloudinaryHelper;
        }

        public async Task<ServiceResponse> GetAllAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var bannerConfigs = await _bannerConfigRepository.GetBannersAsync(page, pageSize, search);
            var total = await _bannerConfigRepository.GetTotalBannersAsync(search);

            var bannerConfigResponses = bannerConfigs.Select(b => new BannerConfigResponse
            {
                BannerId = b.BannerId,
                Title = b.Title,
                ColorCode = b.ColorCode,
                Source = b.Source,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt ?? DateTime.UtcNow
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
                    PageSize = pageSize
                }
            };
        }


        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var bannerConfig = await _bannerConfigRepository.GetByIdAsync(id);
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
                    IsActive = bannerConfig.IsActive,
                    CreatedAt = bannerConfig.CreatedAt ?? DateTime.UtcNow
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

            var bannerConfig = new BannerConfig
            {
                Title = request.Title,
                ColorCode = request.ColorCode,
                Source = imageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _bannerConfigRepository.AddAsync(bannerConfig);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Banner config created successfully.",
            };
        }

        public async Task<ServiceResponse> UpdateAsync(int id, BannerConfigRequest request)
        {
            var bannerConfig = await _bannerConfigRepository.GetByIdAsync(id);
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

            await _bannerConfigRepository.UpdateAsync(bannerConfig);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Banner config updated successfully.",
            };
        }


        public async Task<ServiceResponse> SoftDeleteAsync(int id)
        {
            var bannerConfig = await _bannerConfigRepository.GetByIdAsync(id);
            if (bannerConfig == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Banner config not found."
                };
            }

            bannerConfig.IsActive = false;
            await _bannerConfigRepository.UpdateAsync(bannerConfig);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Banner config deactivated successfully."
            };
        }
    }
}

