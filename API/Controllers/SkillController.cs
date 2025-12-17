using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/skills")]
    [ApiController]
    public class PublicSkillController : ControllerBase
    {
        private readonly ISkillService _skillService;

        public PublicSkillController(ISkillService skillService)
        {
            _skillService = skillService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _skillService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _skillService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/skills")]
    [ApiController]
    public class SystemSkillController : ControllerBase
    {
        private readonly ISkillService _skillService;

        public SystemSkillController(ISkillService skillService)
        {
            _skillService = skillService;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] SkillRequest request)
        {
            var response = await _skillService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] SkillRequest request)
        {
            var response = await _skillService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _skillService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}
