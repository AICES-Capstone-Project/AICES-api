using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/languages")]
    [ApiController]
    public class PublicLanguageController : ControllerBase
    {
        private readonly ILanguageService _languageService;

        public PublicLanguageController(ILanguageService languageService)
        {
            _languageService = languageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _languageService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _languageService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/languages")]
    [ApiController]
    public class SystemLanguageController : ControllerBase
    {
        private readonly ILanguageService _languageService;

        public SystemLanguageController(ILanguageService languageService)
        {
            _languageService = languageService;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] LanguageRequest request)
        {
            var response = await _languageService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] LanguageRequest request)
        {
            var response = await _languageService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _languageService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}

