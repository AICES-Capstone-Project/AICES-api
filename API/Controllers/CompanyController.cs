using API.Common;
using BusinessObjectLayer.IServices;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/companies")]
    [ApiController]
    
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() =>
            ControllerResponse.Response(await _companyService.GetAllAsync());

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id) =>
            ControllerResponse.Response(await _companyService.GetByIdAsync(id));

        [HttpPost]
        [Authorize(Roles = "HR_Recruiter")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Create([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.CreateAsync(request));

        [HttpPatch("{id}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> Update(int id, [FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateAsync(id, request));

        [HttpDelete("{id}")]
        [Authorize(Roles = "HR_Manager")]
        public async Task<IActionResult> Delete(int id) =>
            ControllerResponse.Response(await _companyService.DeleteAsync(id));

        [HttpPatch("{id}/approval")]
        [Authorize(Roles = "System_Manager")]
        public async Task<IActionResult> ApproveOrReject(int id, [FromQuery] bool isApproved)
        {
            var response = await _companyService.ApproveOrRejectAsync(id, isApproved);
            return ControllerResponse.Response(response);
        }

    }
}
