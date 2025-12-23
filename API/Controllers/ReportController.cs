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

        #region Export Endpoints for System Reports

        /// <summary>
        /// Export executive summary to Excel
        /// </summary>
        [HttpGet("executive-summary/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportExecutiveSummaryToExcel()
        {
            var serviceResponse = await _reportService.ExportExecutiveSummaryToExcelAsync();

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

        /// <summary>
        /// Export executive summary to PDF
        /// </summary>
        [HttpGet("executive-summary/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportExecutiveSummaryToPdf()
        {
            var serviceResponse = await _reportService.ExportExecutiveSummaryToPdfAsync();

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

        /// <summary>
        /// Export companies overview to Excel
        /// </summary>
        [HttpGet("companies/overview/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportCompaniesOverviewToExcel()
        {
            var serviceResponse = await _reportService.ExportCompaniesOverviewToExcelAsync();

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

        /// <summary>
        /// Export companies overview to PDF
        /// </summary>
        [HttpGet("companies/overview/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportCompaniesOverviewToPdf()
        {
            var serviceResponse = await _reportService.ExportCompaniesOverviewToPdfAsync();

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

        /// <summary>
        /// Export companies usage to Excel
        /// </summary>
        [HttpGet("companies/usage/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportCompaniesUsageToExcel()
        {
            var serviceResponse = await _reportService.ExportCompaniesUsageToExcelAsync();

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

        /// <summary>
        /// Export companies usage to PDF
        /// </summary>
        [HttpGet("companies/usage/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportCompaniesUsageToPdf()
        {
            var serviceResponse = await _reportService.ExportCompaniesUsageToPdfAsync();

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

        /// <summary>
        /// Export jobs statistics to Excel
        /// </summary>
        [HttpGet("jobs/statistics/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportJobsStatisticsToExcel()
        {
            var serviceResponse = await _reportService.ExportJobsStatisticsToExcelAsync();

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

        /// <summary>
        /// Export jobs statistics to PDF
        /// </summary>
        [HttpGet("jobs/statistics/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportJobsStatisticsToPdf()
        {
            var serviceResponse = await _reportService.ExportJobsStatisticsToPdfAsync();

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

        /// <summary>
        /// Export jobs effectiveness to Excel
        /// </summary>
        [HttpGet("jobs/effectiveness/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportJobsEffectivenessToExcel()
        {
            var serviceResponse = await _reportService.ExportJobsEffectivenessToExcelAsync();

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

        /// <summary>
        /// Export jobs effectiveness to PDF
        /// </summary>
        [HttpGet("jobs/effectiveness/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportJobsEffectivenessToPdf()
        {
            var serviceResponse = await _reportService.ExportJobsEffectivenessToPdfAsync();

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

        /// <summary>
        /// Export AI parsing quality to Excel
        /// </summary>
        [HttpGet("ai/parsing/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportAiParsingQualityToExcel()
        {
            var serviceResponse = await _reportService.ExportAiParsingQualityToExcelAsync();

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

        /// <summary>
        /// Export AI parsing quality to PDF
        /// </summary>
        [HttpGet("ai/parsing/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportAiParsingQualityToPdf()
        {
            var serviceResponse = await _reportService.ExportAiParsingQualityToPdfAsync();

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

        /// <summary>
        /// Export AI scoring distribution to Excel
        /// </summary>
        [HttpGet("ai/scoring/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportAiScoringDistributionToExcel()
        {
            var serviceResponse = await _reportService.ExportAiScoringDistributionToExcelAsync();

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

        /// <summary>
        /// Export AI scoring distribution to PDF
        /// </summary>
        [HttpGet("ai/scoring/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportAiScoringDistributionToPdf()
        {
            var serviceResponse = await _reportService.ExportAiScoringDistributionToPdfAsync();

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

        /// <summary>
        /// Export subscription revenue to Excel
        /// </summary>
        [HttpGet("subscriptions/excel")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportSubscriptionRevenueToExcel()
        {
            var serviceResponse = await _reportService.ExportSubscriptionRevenueToExcelAsync();

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

        /// <summary>
        /// Export subscription revenue to PDF
        /// </summary>
        [HttpGet("subscriptions/pdf")]
        [Authorize(Roles = "System_Admin,System_Manager")]
        public async Task<IActionResult> ExportSubscriptionRevenueToPdf()
        {
            var serviceResponse = await _reportService.ExportSubscriptionRevenueToPdfAsync();

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

        #endregion

        
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
