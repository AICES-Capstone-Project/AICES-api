using API.Common;
using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/system/roles")]
    [ApiController]
    public class SystemRoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public SystemRoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _roleService.GetAllAsync();
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _roleService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}




