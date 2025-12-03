using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace BusinessObjectLayer.Services
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReportService(IUnitOfWork uow, IHttpContextAccessor httpContextAccessor)
        {
            _uow = uow;
            _httpContextAccessor = httpContextAccessor;
        }

        static ReportService()
        {
            // Set EPPlus license for non-commercial use
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<ServiceResponse> ExportJobCandidatesToExcelAsync(int jobId)
        {
            try
            {
                // Get current user and company
                var user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user != null ? ClaimUtils.GetUserIdClaim(user) : null;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);
                var companyUserRepo = _uow.GetRepository<ICompanyUserRepository>();
                var jobRepo = _uow.GetRepository<IJobRepository>();
                var parsedCandidateRepo = _uow.GetRepository<IParsedCandidateRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Validate job exists and belongs to company
                var job = await jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Job not found."
                    };
                }

                if (job.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to export candidates for this job."
                    };
                }

                // Get candidates with their scores and rankings
                var candidates = await parsedCandidateRepo.GetCandidatesWithScoresByJobIdAsync(jobId);

                if (candidates == null || !candidates.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No candidates found for this job."
                    };
                }

                // Create Excel file
                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Candidates Report");

                // Header row
                var headers = new[] { "Rank", "Full Name", "Email", "Score", "Phone", "Matched Skills", "Missing Skills", "AI Explanation" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cells[1, i + 1].Value = headers[i];
                }

                // Style header
                using (var range = ws.Cells[1, 1, 1, headers.Length])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(Color.White);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                }

                // Data rows - sorted by rank
                var sortedCandidates = candidates
                    .OrderBy(c => c.RankingResult?.RankPosition ?? int.MaxValue)
                    .ThenByDescending(c => c.AIScores?.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.TotalResumeScore ?? 0)
                    .ToList();

                int row = 2;
                foreach (var c in sortedCandidates)
                {
                    var latestScore = c.AIScores?.OrderByDescending(s => s.CreatedAt).FirstOrDefault();

                    ws.Cells[row, 1].Value = c.RankingResult?.RankPosition ?? row - 1;
                    ws.Cells[row, 2].Value = c.FullName;
                    ws.Cells[row, 3].Value = c.Email;
                    ws.Cells[row, 4].Value = latestScore?.TotalResumeScore ?? 0;
                    ws.Cells[row, 5].Value = c.PhoneNumber ?? "N/A";
                    ws.Cells[row, 6].Value = c.MatchSkills ?? "";
                    ws.Cells[row, 7].Value = c.MissingSkills ?? "";
                    ws.Cells[row, 8].Value = latestScore?.AIExplanation ?? "";

                    row++;
                }

                // Auto-fit columns
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // Set minimum width for certain columns
                ws.Column(6).Width = Math.Max(ws.Column(6).Width, 30); // Matched Skills
                ws.Column(7).Width = Math.Max(ws.Column(7).Width, 30); // Missing Skills
                ws.Column(8).Width = Math.Max(ws.Column(8).Width, 50); // AI Explanation

                // Wrap text for long content columns
                ws.Column(6).Style.WrapText = true;
                ws.Column(7).Style.WrapText = true;
                ws.Column(8).Style.WrapText = true;

                // Add borders to all data
                using (var range = ws.Cells[1, 1, row - 1, headers.Length])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Candidates_Job_{jobId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Excel file generated successfully.",
                    Data = new ExcelExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error exporting candidates to Excel: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while exporting candidates to Excel."
                };
            }
        }
    }
}
