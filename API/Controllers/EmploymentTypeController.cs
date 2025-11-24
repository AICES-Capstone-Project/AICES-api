using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/employment-types")]
    [ApiController]
    public class PublicEmploymentTypeController : ControllerBase
    {
        private readonly IEmploymentTypeService _service;

        public PublicEmploymentTypeController(IEmploymentTypeService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _service.GetAllAsync();
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/employment-types")]
    [ApiController]
    public class SystemEmploymentTypeController : ControllerBase
    {
        private readonly IEmploymentTypeService _service;

        public SystemEmploymentTypeController(IEmploymentTypeService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] EmploymentTypeRequest request)
        {
            var response = await _service.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] EmploymentTypeRequest request)
        {
            var response = await _service.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _service.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}
