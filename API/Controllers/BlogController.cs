using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/blogs")]
    [ApiController]
    public class BlogController : ControllerBase
    {
        private readonly IBlogService _blogService;

        public BlogController(IBlogService blogService)
        {
            _blogService = blogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBlogs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _blogService.GetAllBlogsAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBlogById(int id)
        {
            var serviceResponse = await _blogService.GetBlogByIdAsync(id);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBlogBySlug(string slug)
        {
            var serviceResponse = await _blogService.GetBlogBySlugAsync(slug);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("me")]
        [Authorize(Roles = "System_Manager, System_Admin, System_Staff")]
        public async Task<IActionResult> GetMyBlogs([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _blogService.GetMyBlogsAsync(User, page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPost]
        [Authorize(Roles = "System_Manager, System_Admin, System_Staff")]
        public async Task<IActionResult> CreateBlog([FromBody] BlogRequest request)
        {
            var serviceResponse = await _blogService.CreateBlogAsync(request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "System_Manager, System_Admin, System_Staff")]
        public async Task<IActionResult> UpdateBlog(int id, [FromBody] BlogRequest request)
        {
            var serviceResponse = await _blogService.UpdateBlogAsync(id, request, User);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Manager, System_Admin, System_Staff")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            var serviceResponse = await _blogService.DeleteBlogAsync(id, User);
            return ControllerResponse.Response(serviceResponse);
        }
    }
}

