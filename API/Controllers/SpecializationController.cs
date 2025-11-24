using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/specializations")]
    [ApiController]
    public class PublicSpecializationController : ControllerBase
    {
        private readonly ISpecializationService _specializationService;

        public PublicSpecializationController(ISpecializationService specializationService)
        {
            _specializationService = specializationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _specializationService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _specializationService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/specializations")]
    [ApiController]
    public class SystemSpecializationController : ControllerBase
    {
        private readonly ISpecializationService _specializationService;

        public SystemSpecializationController(ISpecializationService specializationService)
        {
            _specializationService = specializationService;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] SpecializationRequest request)
        {
            var response = await _specializationService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] SpecializationRequest request)
        {
            var response = await _specializationService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _specializationService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}

