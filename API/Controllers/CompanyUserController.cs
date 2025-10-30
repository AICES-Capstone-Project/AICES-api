using API.Common;
using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/company-user")]
    [ApiController]
    [Authorize]
    public class CompanyUserController : ControllerBase
    {
        private readonly ICompanyUserService _companyUserService;

        public CompanyUserController(ICompanyUserService companyUserService)
        {
            _companyUserService = companyUserService;
        }

        [HttpGet("{companyId}/members/self")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> GetSelfCompanyMembers(int companyId)
        {
            var response = await _companyUserService.GetMembersByCompanyIdAsync(companyId);
            return ControllerResponse.Response(response);
        }
    }
}


