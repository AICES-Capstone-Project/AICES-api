using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/categories")]
    [ApiController]
    
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ISpecializationService _specializationService;

        public CategoryController(ICategoryService categoryService, ISpecializationService specializationService)
        {
            _categoryService = categoryService;
            _specializationService = specializationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var response = await _categoryService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _categoryService.GetByIdAsync(id);
            return ControllerResponse.Response(response);
        }

        [HttpGet("{categoryId}/specializations")]
        public async Task<IActionResult> GetSpecializationsByCategoryId(int categoryId)
        {
            var response = await _specializationService.GetByCategoryIdAsync(categoryId);
            return ControllerResponse.Response(response);
        }

        [HttpPost]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Create([FromBody] CategoryRequest request)
        {
            var response = await _categoryService.CreateAsync(request);
            return ControllerResponse.Response(response);
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
        {
            var response = await _categoryService.UpdateAsync(id, request);
            return ControllerResponse.Response(response);
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin,System_Manager,System_Staff")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var response = await _categoryService.SoftDeleteAsync(id);
            return ControllerResponse.Response(response);
        }
    }
}
