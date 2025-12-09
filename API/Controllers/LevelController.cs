using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/levels")]
    [ApiController]
    public class PublicLevelController : ControllerBase
    {
        private readonly ILevelService _levelService;

        public PublicLevelController(ILevelService levelService)
        {
            _levelService = levelService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _levelService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _levelService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/levels")]
    [ApiController]
    public class SystemLevelController : ControllerBase
    {
        private readonly ILevelService _levelService;

        public SystemLevelController(ILevelService levelService)
        {
            _levelService = levelService;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] LevelRequest request)
        {
            var response = await _levelService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] LevelRequest request)
        {
            var response = await _levelService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _levelService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}
