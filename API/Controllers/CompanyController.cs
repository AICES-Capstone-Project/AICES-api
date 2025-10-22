using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
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

        [HttpGet("self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompany() =>
            ControllerResponse.Response(await _companyService.GetSelfCompanyAsync());

        [HttpPatch("{id}/profile")]
        [Authorize(Roles = "System_Admin, System_Manager, HR_Manager")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateProfile(int id, [FromForm] CompanyProfileUpdateRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateCompanyProfileAsync(id, request));

        [HttpPost("self")]
        [Authorize(Roles = "HR_Recruiter")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SelfCreate([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.SelfCreateAsync(request));

        [HttpPatch("self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateSelfCompany([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateSelfCompanyAsync(request));

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic() =>
            ControllerResponse.Response(await _companyService.GetPublicAsync());

        [HttpGet("public/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicById(int id) =>
            ControllerResponse.Response(await _companyService.GetPublicByIdAsync(id));

        [HttpGet]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var serviceResponse = await _companyService.GetAllAsync(page, pageSize, search);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff, HR_Manager")]
        public async Task<IActionResult> GetById(int id) =>
            ControllerResponse.Response(await _companyService.GetByIdAsync(id));

        [HttpPost]
        [Authorize(Roles = "System_Admin, System_Manager")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Create([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.CreateAsync(request));

        [HttpPatch("{id}")]
        [Authorize(Roles = "System_Admin, System_Manager")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Update(int id, [FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateAsync(id, request));

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin, System_Manager")]
        public async Task<IActionResult> Delete(int id) =>
            ControllerResponse.Response(await _companyService.DeleteAsync(id));

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "System_Admin, System_Manager")]
        public async Task<IActionResult> UpdateCompanyStatus(int id, [FromQuery] CompanyStatusEnum status, [FromQuery] string? rejectionReason = null)
        {
            var response = await _companyService.UpdateCompanyStatusAsync(id, status, rejectionReason);
            return ControllerResponse.Response(response);
        }

    }
}
