using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/public/companies")]
    [ApiController]
    public class PublicCompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public PublicCompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic() =>
            ControllerResponse.Response(await _companyService.GetPublicAsync());

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicById(int id) =>
            ControllerResponse.Response(await _companyService.GetPublicByIdAsync(id));
    }

    [Route("api/companies")]
    [ApiController]
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet("rejected")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> GetRejectedSelfCompany() =>
            ControllerResponse.Response(await _companyService.GetRejectedSelfCompanyAsync());

        [HttpGet("profile")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompany() =>
            ControllerResponse.Response(await _companyService.GetSelfCompanyAsync());

        [HttpPatch("profile")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateProfile([FromForm] CompanyProfileUpdateRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateCompanyProfileAsync(request));

        // Register new company
        [HttpPost]
        [Authorize(Roles = "HR_Recruiter, HR_Manager")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SelfCreate([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.SelfCreateAsync(request));

        // Update company to resend after rejection
        [HttpPatch]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateSelfCompany([FromForm] CompanyRequest request) =>
            ControllerResponse.Response(await _companyService.UpdateSelfCompanyAsync(request));

        // Cancel company registration
        [HttpPut("cancel")]
        [Authorize(Roles = "HR_Recruiter")]
        public async Task<IActionResult> CancelCompany()
        {
            var response = await _companyService.CancelCompanyAsync();
            return ControllerResponse.Response(response);
        }
    }

    [Route("api/system/companies")]
    [ApiController]
    public class SystemCompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public SystemCompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] CompanyStatusEnum? status = null)
        {
            var serviceResponse = await _companyService.GetAllAsync(page, pageSize, search, status);
            return ControllerResponse.Response(serviceResponse);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "System_Admin, System_Manager, System_Staff, HR_Manager")]
        public async Task<IActionResult> GetById(int id) =>
            ControllerResponse.Response(await _companyService.GetByIdAsync(id));

        // [HttpPost]
        // [Authorize(Roles = "System_Admin, System_Manager")]
        // [RequestSizeLimit(10_000_000)]
        // public async Task<IActionResult> Create([FromForm] CompanyRequest request) =>
        //     ControllerResponse.Response(await _companyService.CreateAsync(request));

        // [HttpPatch("{id}")]
        // [Authorize(Roles = "System_Admin, System_Manager")]
        // [RequestSizeLimit(10_000_000)]
        // public async Task<IActionResult> Update(int id, [FromForm] CompanyRequest request) =>
        //     ControllerResponse.Response(await _companyService.UpdateAsync(id, request));

        [HttpDelete("{id}")]
        [Authorize(Roles = "System_Admin, System_Manager")]
        public async Task<IActionResult> Delete(int id) =>
            ControllerResponse.Response(await _companyService.DeleteAsync(id));

        [HttpPut("{id}/status")]
        [Authorize(Roles = "System_Admin, System_Manager")]
        public async Task<IActionResult> UpdateCompanyStatus(int id, [FromBody] UpdateCompanyStatusRequest request)
        {
            var response = await _companyService.UpdateCompanyStatusAsync(id, request.Status, request.RejectionReason);
            return ControllerResponse.Response(response);
        }
    }
}
