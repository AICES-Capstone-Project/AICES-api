using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobSkillController : ControllerBase
    {
        private readonly IJobSkillService _jobSkillService;

        public JobSkillController(IJobSkillService jobSkillService)
        {
            _jobSkillService = jobSkillService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _jobSkillService.GetAllAsync();
            return StatusCode((int)result.Status, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _jobSkillService.GetByIdAsync(id);
            return StatusCode((int)result.Status, result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JobSkillRequest request)
        {
            var result = await _jobSkillService.CreateAsync(request);
            return StatusCode((int)result.Status, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] JobSkillRequest request)
        {
            var result = await _jobSkillService.UpdateAsync(id, request);
            return StatusCode((int)result.Status, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _jobSkillService.DeleteAsync(id);
            return StatusCode((int)result.Status, result);
        }
    }
}
