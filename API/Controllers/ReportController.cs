using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/system/reports")]
    [ApiController]
    public class SystemReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public SystemReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        
        [HttpGet("executive-summary")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetExecutiveSummary()
        {
            var serviceResponse = await _reportService.GetExecutiveSummaryAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        
        [HttpGet("companies/overview")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetCompaniesOverview()
        {
            var serviceResponse = await _reportService.GetCompaniesOverviewAsync();
            return ControllerResponse.Response(serviceResponse);
        }

      
        [HttpGet("companies/usage")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetCompaniesUsage()
        {
            var serviceResponse = await _reportService.GetCompaniesUsageAsync();
            return ControllerResponse.Response(serviceResponse);
        }

      
        [HttpGet("jobs/statistics")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetJobsStatistics()
        {
            var serviceResponse = await _reportService.GetJobsStatisticsAsync();
            return ControllerResponse.Response(serviceResponse);
        }

   
        [HttpGet("jobs/effectiveness")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetJobsEffectiveness()
        {
            var serviceResponse = await _reportService.GetJobsEffectivenessAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// Get AI resume parsing quality report
        /// </summary>
        [HttpGet("ai/parsing")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetAiParsingQuality()
        {
            var serviceResponse = await _reportService.GetAiParsingQualityAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// Get AI scoring distribution report
        /// </summary>
        [HttpGet("ai/scoring")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetAiScoringDistribution()
        {
            var serviceResponse = await _reportService.GetAiScoringDistributionAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        /// <summary>
        /// Get subscription and revenue report
        /// </summary>
        [HttpGet("subscriptions")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> GetSubscriptionRevenue()
        {
            var serviceResponse = await _reportService.GetSubscriptionRevenueAsync();
            return ControllerResponse.Response(serviceResponse);
        }

        
        [HttpGet("campaigns/{campaignId}/job/{jobId}/excel")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> ExportJobCandidatesToExcel(int campaignId, int jobId)
        {
            var serviceResponse = await _reportService.ExportJobCandidatesToExcelAsync(campaignId, jobId);

            if (serviceResponse.Status != SRStatus.Success)
            {
                return ControllerResponse.Response(serviceResponse);
            }

            var excelData = serviceResponse.Data as ExcelExportResponse;
            if (excelData == null)
            {
                return StatusCode(500, "Failed to generate Excel file.");
            }

            return File(excelData.FileBytes, excelData.ContentType, excelData.FileName);
        }

        
        [HttpGet("campaigns/{campaignId}/job/{jobId}/pdf")]
        [Authorize(Roles = "HR_Manager, HR_Recruiter")]
        public async Task<IActionResult> ExportJobCandidatesToPdf(int campaignId, int jobId)
        {
            var serviceResponse = await _reportService.ExportJobCandidatesToPdfAsync(campaignId, jobId);

            if (serviceResponse.Status != SRStatus.Success)
            {
                return ControllerResponse.Response(serviceResponse);
            }

            var pdfData = serviceResponse.Data as PdfExportResponse;
            if (pdfData == null)
            {
                return StatusCode(500, "Failed to generate PDF report.");
            }

            return File(pdfData.FileBytes, pdfData.ContentType, pdfData.FileName);
        }
    }
}
