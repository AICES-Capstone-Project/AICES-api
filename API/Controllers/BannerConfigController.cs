using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/banner-configs")]
    [ApiController]
    public class PublicBannerConfigController : ControllerBase
    {
        private readonly IBannerConfigService _bannerConfigService;

        public PublicBannerConfigController(IBannerConfigService bannerConfigService)
        {
            _bannerConfigService = bannerConfigService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _bannerConfigService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _bannerConfigService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/banner-configs")]
    [ApiController]
    public class SystemBannerConfigController : ControllerBase
    {
        private readonly IBannerConfigService _bannerConfigService;

        public SystemBannerConfigController(IBannerConfigService bannerConfigService)
        {
            _bannerConfigService = bannerConfigService;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromForm] BannerConfigRequest request)
        {
            var response = await _bannerConfigService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromForm] BannerConfigRequest request)
        {
            var response = await _bannerConfigService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _bannerConfigService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}

