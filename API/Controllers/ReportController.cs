using API.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Export candidates of a job to Excel file
        /// </summary>
        /// <param name="jobId">Job ID</param>
        /// <returns>Excel file with candidate data</returns>
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

        /// <summary>
        /// Export candidates of a job to PDF report
        /// </summary>
        /// <param name="campaignId">Campaign ID</param>
        /// <param name="jobId">Job ID</param>
        /// <returns>PDF file with detailed candidate report</returns>
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
