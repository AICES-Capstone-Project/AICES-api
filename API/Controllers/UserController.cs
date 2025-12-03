        using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/system/users")]
    [ApiController]
    [Authorize(Roles = "System_Admin")] 
    public class SystemUserController : ControllerBase
    {
        private readonly IUserService _userService;

        public SystemUserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var serviceResponse = await _userService.CreateUserAsync(request);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _userService.GetUsersAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var serviceResponse = await _userService.GetUserByIdAsync(id);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var serviceResponse = await _userService.UpdateUserAsync(id, request);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteUser(int id)
        {
            var serviceResponse = await _userService.SoftDeleteAsync(id);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
        {
            var serviceResponse = await _userService.UpdateUserStatusAsync(id, request.Status);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}
