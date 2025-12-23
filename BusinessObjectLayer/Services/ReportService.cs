using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using DataAccessLayer.Repositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Drawing;

namespace BusinessObjectLayer.Services
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _uow;
        private readonly AICESDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IReportRepository _reportRepository;

        public ReportService(IUnitOfWork uow, AICESDbContext context, IHttpContextAccessor httpContextAccessor, IWebHostEnvironment webHostEnvironment)
        {
            _uow = uow;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _webHostEnvironment = webHostEnvironment;
            _reportRepository = _uow.GetRepository<IReportRepository>();
        }

        static ReportService()
        {
            // Set EPPlus license for non-commercial use
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // Set QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<ServiceResponse> ExportJobCandidatesToExcelAsync(int campaignId, int jobId)
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
                var campaignRepo = _uow.GetRepository<ICampaignRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                var campaign = await campaignRepo.GetByIdAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                if (campaign.CompanyId != companyUser.CompanyId.Value)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to export reports for this campaign."
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

                var jobCampaign = await campaignRepo.GetJobCampaignByJobIdAndCampaignIdAsync(jobId, campaignId);
                if (jobCampaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "The specified job does not belong to this campaign."
                    };
                }

                // Check if company has active paid subscription (Free plan cannot export)
                var subscriptionCheck = await CheckIfCompanyHasPaidSubscriptionAsync(companyUser.CompanyId.Value, "Excel export");
                if (subscriptionCheck != null)
                    return subscriptionCheck;

                var resumeApplications = await _context.ResumeApplications
                    .AsNoTracking()
                    .Where(ra => ra.JobId == jobId
                                 && ra.CampaignId == campaignId
                                 && ra.IsActive)
                    .Include(ra => ra.Resume)
                        .ThenInclude(r => r.Candidate)
                    .ToListAsync();

                if (resumeApplications == null || !resumeApplications.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No candidates found for this job and campaign."
                    };
                }

                var sortedApplications = resumeApplications
                    .OrderByDescending(ra => ra.AdjustedScore ?? ra.TotalScore ?? 0m)
                    .ThenBy(ra => ra.CreatedAt ?? DateTime.MinValue)
                    .ToList();

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
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                }

                int row = 2;
                int rank = 1;
                foreach (var application in sortedApplications)
                {
                    var candidate = application.Resume?.Candidate;
                    ws.Cells[row, 1].Value = rank;
                    ws.Cells[row, 2].Value = candidate?.FullName ?? "Unknown";
                    ws.Cells[row, 3].Value = candidate?.Email ?? "N/A";
                    ws.Cells[row, 4].Value = (double)(application?.AdjustedScore ?? application?.TotalScore ?? 0m);
                    ws.Cells[row, 5].Value = candidate?.PhoneNumber ?? "N/A";
                    ws.Cells[row, 6].Value = application?.MatchSkills ?? string.Empty;
                    ws.Cells[row, 7].Value = application?.MissingSkills ?? string.Empty;
                    ws.Cells[row, 8].Value = application?.AIExplanation ?? string.Empty;

                    row++;
                    rank++;
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
                var fileName = $"Candidates_Campaign_{campaignId}_Job_{jobId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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

        public async Task<ServiceResponse> ExportJobCandidatesToPdfAsync(int campaignId, int jobId)
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
                var companyRepo = _uow.GetRepository<ICompanyRepository>();
                var campaignRepo = _uow.GetRepository<ICampaignRepository>();

                var companyUser = await companyUserRepo.GetByUserIdAsync(userId);

                if (companyUser == null || companyUser.CompanyId == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found for user."
                    };
                }

                // Get company info
                var company = await companyRepo.GetByIdAsync(companyUser.CompanyId.Value);
                if (company == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Company not found."
                    };
                }

                // Validate campaign
                var campaign = await campaignRepo.GetByIdAsync(campaignId);
                if (campaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Campaign not found."
                    };
                }

                if (campaign.CompanyId != company.CompanyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to export reports for this campaign."
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

                if (job.CompanyId != company.CompanyId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Forbidden,
                        Message = "You do not have permission to export candidates for this job."
                    };
                }

                var jobCampaign = await campaignRepo.GetJobCampaignByJobIdAndCampaignIdAsync(jobId, campaignId);
                if (jobCampaign == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "The specified job does not belong to this campaign."
                    };
                }

                // Check if company has active paid subscription (Free plan cannot export PDF)
                var subscriptionCheck = await CheckIfCompanyHasPaidSubscriptionAsync(company.CompanyId, "PDF export");
                if (subscriptionCheck != null)
                    return subscriptionCheck;

                var resumeApplications = await _context.ResumeApplications
                    .AsNoTracking()
                    .Where(ra => ra.JobId == jobId
                                 && ra.CampaignId == campaignId
                                 && ra.IsActive)
                    .Include(ra => ra.Resume)
                        .ThenInclude(r => r.Candidate)
                    .Include(ra => ra.ScoreDetails)
                        .ThenInclude(sd => sd.Criteria)
                    .Include(ra => ra.Job)
                    .ToListAsync();

                if (resumeApplications == null || !resumeApplications.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "No candidates found for this job and campaign."
                    };
                }

                var totalCandidates = resumeApplications.Count;
                var parsedCount = resumeApplications.Count(ra => ra.Resume?.Status == ResumeStatusEnum.Completed);
                var scoredCount = resumeApplications.Count(ra => ra.TotalScore.HasValue || ra.AdjustedScore.HasValue);
                var shortlistedCount = scoredCount;

                var sortedApplications = resumeApplications
                    .OrderByDescending(ra => ra.AdjustedScore ?? ra.TotalScore ?? 0m)
                    .ThenBy(ra => ra.CreatedAt ?? DateTime.MinValue)
                    .ToList();

                var top5ResumeApplications = sortedApplications.Take(5).ToList();

                // Generate PDF
                var pdfDocument = Document.Create(container =>
                {
                    // PAGE 1: Cover + Overview Dashboard
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeCoverHeader(c));

                        page.Content().Element(c => ComposeCoverContent(c, company.Name, job, totalCandidates, parsedCount, scoredCount, shortlistedCount, top5ResumeApplications));

                        page.Footer().Element(c => ComposeFooter(c, 1));
                    });

                    // PAGE 2 to N: Individual Candidate Pages
                    int pageNumber = 2;
                    int rank = 1;
                    foreach (var application in sortedApplications)
                    {
                        var currentRank = rank;
                        var currentPage = pageNumber;
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(40);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Header().Element(c => ComposeCandidateHeader(c, application, currentRank));

                            page.Content().Element(c => ComposeCandidateContent(c, application));

                            page.Footer().Element(c => ComposeFooter(c, currentPage));
                        });
                        rank++;
                        pageNumber++;
                    }
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Recruitment_Report_Campaign_{campaignId}_Job_{jobId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error exporting candidates to PDF: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while exporting candidates to PDF."
                };
            }
        }

        #region Subscription Check Helper Methods

        private async Task<ServiceResponse?> CheckIfCompanyHasPaidSubscriptionAsync(int companyId, string exportType = "export")
        {
            var companySubRepo = _uow.GetRepository<ICompanySubscriptionRepository>();
            var companySubscription = await companySubRepo.GetAnyActiveSubscriptionByCompanyAsync(companyId);
            
            if (companySubscription == null)
            {
                // No active subscription means Free plan
                return new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = $"{exportType} feature is only available for paid subscriptions. Please upgrade your plan to {exportType} reports."
                };
            }

            // Check if subscription is Free (additional check)
            var subscriptionRepo = _uow.GetRepository<ISubscriptionRepository>();
            var subscription = await subscriptionRepo.GetByIdAsync(companySubscription.SubscriptionId);
            var freeSubscription = await subscriptionRepo.GetFreeSubscriptionAsync();
            
            if (freeSubscription != null && subscription != null && subscription.SubscriptionId == freeSubscription.SubscriptionId)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Forbidden,
                    Message = $"{exportType} feature is only available for paid subscriptions. Please upgrade your plan to {exportType} reports."
                };
            }

            return null; // Company has paid subscription
        }

        #endregion

        #region PDF Helper Methods

        private void ComposeCoverHeader(IContainer container)
        {
            var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "avatars", "images", "logo.png");

            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    // Logo thay thế chữ AICES
                    if (File.Exists(logoPath))
                    {
                        try
                        {
                            col.Item().Width(100).Height(100).Image(logoPath);
                        }
                        catch
                        {
                            // Nếu không load được logo, hiển thị text thay thế
                            col.Item().Text("AICES")
                                .FontSize(36)
                                .Bold()
                                .FontColor(Colors.Green.Darken2);
                        }
                    }
                    else
                    {
                        col.Item().Text("AICES")
                            .FontSize(36)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);
                    }

                    col.Item().Text("AI-Powered Candidate Evaluation System")
                        .FontSize(12)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void ComposeCoverContent(IContainer container, string companyName, Job job, int totalCandidates, int parsedCount, int scoredCount, int shortlistedCount, List<ResumeApplication> top5Applications)
        {
            container.PaddingVertical(20).Column(col =>
            {
                // Company & Report Info
                col.Item().PaddingBottom(30).Column(info =>
                {
                    info.Item().Text($"Recruitment Report for: {companyName}")
                        .FontSize(20)
                        .Bold();

                    info.Item().Text($"Generated on: {DateTime.UtcNow:MMMM dd, yyyy}")
                        .FontSize(12)
                        .FontColor(Colors.Grey.Darken1);
                });

                // Job Information Section
                col.Item().PaddingBottom(20).Element(c => ComposeJobInfoSection(c, job, totalCandidates, parsedCount, scoredCount, shortlistedCount));

                // Top 5 Candidates Section
                col.Item().Element(c => ComposeTop5Section(c, top5Applications));
            });
        }

        private void ComposeJobInfoSection(IContainer container, Job job, int totalCandidates, int parsedCount, int scoredCount, int shortlistedCount)
        {
            container.Background(Colors.Grey.Lighten4).Padding(15).Column(col =>
            {
                col.Item().Text("Job Information")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(t =>
                        {
                            t.Span("Job Title: ").Bold();
                            t.Span(job.Title);
                        });

                        left.Item().Text(t =>
                        {
                            t.Span("Job ID: ").Bold();
                            t.Span(job.JobId.ToString());
                        });
                    });

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Text(t =>
                        {
                            t.Span("Total Candidates: ").Bold();
                            t.Span(totalCandidates.ToString());
                        });

                        right.Item().Text(t =>
                        {
                            t.Span("Parsed: ").Bold();
                            t.Span(parsedCount.ToString());
                        });

                        right.Item().Text(t =>
                        {
                            t.Span("Scored: ").Bold();
                            t.Span(scoredCount.ToString());
                        });

                        right.Item().Text(t =>
                        {
                            t.Span("Shortlisted: ").Bold();
                            t.Span(shortlistedCount.ToString());
                        });
                    });
                });
            });
        }

        private void ComposeTop5Section(IContainer container, List<ResumeApplication> resumeApplications)
        {
            container.Background(Colors.Grey.Lighten5).Padding(15).Column(col =>
            {
                col.Item().Text("Top 5 Candidates")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);   // Rank (wider to prevent wrap)
                        columns.RelativeColumn(2);    // Name
                        columns.RelativeColumn(3);    // Email
                        columns.ConstantColumn(60);   // Score
                        columns.ConstantColumn(80);   // Score Bar (fixed width)
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("Rank").Bold().FontColor(Colors.White);
                        header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("Name").Bold().FontColor(Colors.White);
                        header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("Email").Bold().FontColor(Colors.White);
                        header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("Score").Bold().FontColor(Colors.White);
                        header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("").Bold().FontColor(Colors.White);
                    });

                    // Data rows
                    int displayRank = 1;
                    foreach (var application in resumeApplications.Take(5))
                    {
                        var candidate = application.Resume?.Candidate;
                        var score = (float)(application.AdjustedScore ?? application.TotalScore ?? 0);
                        var bgColor = displayRank % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                        
                        // Color based on score range: 0-20 red, 20-40 orange, 40-60 yellow, 60-80 light green, 80-100 green
                        var barColorHex = GetScoreColor(score);

                        table.Cell().Background(bgColor).Padding(8).AlignCenter().Text(displayRank.ToString());
                        table.Cell().Background(bgColor).Padding(8).Text(candidate?.FullName ?? "N/A");
                        table.Cell().Background(bgColor).Padding(8).Text(candidate?.Email ?? "N/A");
                        table.Cell().Background(bgColor).Padding(8).AlignCenter().Text($"{score:F1}");
                        
                        // Score bar - NO PADDING to avoid overflow
                        table.Cell().Background(bgColor).PaddingVertical(8).Row(barRow =>
                        {
                            // Max 80px total (column width), so scale score to 0-80
                            var barWidth = (int)((score / 100f) * 80f);
                            barWidth = Math.Max(4, Math.Min(barWidth, 76)); // 4-76 range to always have visible empty part
                            var emptyWidth = 80 - barWidth;

                            barRow.ConstantItem(barWidth).Height(16)
                                .Background(QuestPDF.Infrastructure.Color.FromHex(barColorHex));
                            barRow.ConstantItem(emptyWidth).Height(16)
                                .Background(Colors.Grey.Lighten3);
                        });

                        displayRank++;
                    }
                });
            });
        }

        // Get color based on score range
        private string GetScoreColor(float score)
        {
            if (score >= 80) return "#4CAF50"; // Green
            if (score >= 60) return "#8BC34A"; // Light Green
            if (score >= 40) return "#FFEB3B"; // Yellow
            if (score >= 20) return "#FF9800"; // Orange
            return "#F44336"; // Red
        }

        private void ComposeCandidateHeader(IContainer container, ResumeApplication application, int rank)
        {
            var candidate = application.Resume?.Candidate;
            var score = application.AdjustedScore ?? application.TotalScore ?? 0m;

            container.Background(Colors.Green.Darken2).Padding(15).Row(row =>
            {
                row.RelativeItem().Text($"Candidate #{rank} — {candidate?.FullName ?? "Unknown"}")
                    .FontSize(18)
                    .Bold()
                    .FontColor(Colors.White);

                row.ConstantItem(120).AlignRight().Text($"Score: {score:F1}")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.White);
            });
        }

        private void ComposeCandidateContent(IContainer container, ResumeApplication application)
        {
            var candidate = application.Resume?.Candidate;
            var resume = application.Resume;
            container.PaddingVertical(15).Column(col =>
            {
                // Basic Info Section
                col.Item().PaddingBottom(15).Element(c => ComposeCandidateBasicInfo(c, candidate));

                // AI Score Breakdown Section
                col.Item().PaddingBottom(15).Element(c => ComposeScoreBreakdown(c, application));

                // Matched Skills Section
                col.Item().PaddingBottom(15).Element(c => ComposeSkillsSection(c, "Matched Skills", application.MatchSkills, Colors.Green.Lighten4));

                // Missing Skills Section
                col.Item().PaddingBottom(15).Element(c => ComposeSkillsSection(c, "Missing Skills", application.MissingSkills, Colors.Red.Lighten4));

                // AI Summary Section
                col.Item().Element(c => ComposeAISummary(c, application.AIExplanation));
            });
        }

        private void ComposeCandidateBasicInfo(IContainer container, Candidate? candidate)
        {
            container.Background(Colors.Grey.Lighten4).Padding(12).Column(col =>
            {
                col.Item().Text("Candidate Basic Info")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(t =>
                        {
                            t.Span("Full Name: ").Bold();
                            t.Span(candidate?.FullName ?? "Unknown");
                        });

                        left.Item().Text(t =>
                        {
                            t.Span("Email: ").Bold();
                            t.Span(candidate?.Email ?? "N/A");
                        });
                    });

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Text(t =>
                        {
                            t.Span("Phone: ").Bold();
                            t.Span(candidate?.PhoneNumber ?? "N/A");
                        });
                    });
                });
            });
        }

        private void ComposeScoreBreakdown(IContainer container, ResumeApplication application)
        {
            container.Background(Colors.Blue.Lighten5).Padding(12).Column(col =>
            {
                // Header with icon
                col.Item().Row(headerRow =>
                {
                    headerRow.RelativeItem().AlignMiddle().Text("AI Score Breakdown")
                        .FontSize(14)
                        .Bold()
                        .FontColor(Colors.Green.Darken2);
                });

                col.Item().PaddingTop(8).Row(row =>
                {
                    // Left side - Total Score with progress bar
                    row.RelativeItem(1).Column(leftCol =>
                    {
                        var totalScore = (float)(application.AdjustedScore ?? application.TotalScore ?? 0);
                        var totalScoreColorHex = GetScoreColor(totalScore);
                        
                        leftCol.Item().Text(t =>
                        {
                            t.Span("Total Score: ").Bold();
                            t.Span($"{totalScore:F1}").FontSize(14).Bold().FontColor(QuestPDF.Infrastructure.Color.FromHex(totalScoreColorHex));
                            t.Span(" / 100").FontSize(14).Bold().FontColor(Colors.Black);
                        });

                        // Progress bar for Total Score with dynamic color
                        var scorePercent = Math.Max(0, Math.Min(totalScore / 100f, 1f));

                        leftCol.Item().PaddingTop(5).Width(200).Row(progressRow =>
                        {
                            if (scorePercent > 0.01f) // At least 1%
                            {
                                progressRow.RelativeItem((float)(scorePercent * 100))
                                    .Height(12)
                                    .Background(QuestPDF.Infrastructure.Color.FromHex(totalScoreColorHex));
                            }
                            if (scorePercent < 0.99f) // Less than 99%
                            {
                                progressRow.RelativeItem((float)((1f - scorePercent) * 100))
                                    .Height(12)
                                    .Background(Colors.Grey.Lighten3);
                            }
                        });
                    });

                    // Right side - Criteria Scores with color indicators and progress bars
                    row.RelativeItem(1).Column(rightCol =>
                    {
                        rightCol.Item().Text("Criteria Scores:").Bold().FontSize(11);

                        if (application.ScoreDetails != null && application.ScoreDetails.Any())
                        {
                            rightCol.Item().PaddingTop(8).Column(criteriaCol =>
                            {
                                foreach (var detail in application.ScoreDetails)
                                {
                                    var criteriaScore = (float)detail.Score;
                                    var criteriaPercent = Math.Min(criteriaScore / 100f, 1f);
                                    
                                    // Get color based on score percentage using same 5-tier system as Top 5
                                    var colorHex = GetScoreColor(criteriaScore);

                                    criteriaCol.Item().PaddingVertical(3).Column(criteriaItemCol =>
                                    {
                                        // Criteria name with color box and score
                                        criteriaItemCol.Item().Row(criteriaRow =>
                                        {
                                            // Color box
                                            criteriaRow.ConstantItem(12).Height(12)
                                                .Background(QuestPDF.Infrastructure.Color.FromHex(colorHex));

                                            criteriaRow.ConstantItem(5); // Spacing

                                            // Criteria name and score with dynamic color
                                            var criteriaName = detail.Criteria?.Name ?? "Criteria";
                                            criteriaRow.RelativeItem().Text(t =>
                                            {
                                                t.Span($"{criteriaName}: ").FontSize(10);
                                                t.Span($"{detail.Score:F0}%").FontSize(10).FontColor(QuestPDF.Infrastructure.Color.FromHex(colorHex));
                                            });
                                        });

                                        // Small progress bar for this criteria with dynamic color
                                        criteriaItemCol.Item().PaddingTop(2).Width(150).Row(miniProgressRow =>
                                        {
                                            if (criteriaPercent > 0.01f) // At least 1%
                                            {
                                                miniProgressRow.RelativeItem((float)(criteriaPercent * 100))
                                                    .Height(4)
                                                    .Background(QuestPDF.Infrastructure.Color.FromHex(colorHex));
                                            }
                                            if (criteriaPercent < 0.99f) // Less than 99%
                                            {
                                                miniProgressRow.RelativeItem((float)((1f - criteriaPercent) * 100))
                                                    .Height(4)
                                                    .Background(Colors.Grey.Lighten3);
                                            }
                                        });
                                    });
                                }
                            });
                        }
                    });
                });
            });
        }

        private void ComposeSkillsSection(IContainer container, string title, string? skills, string bgColor)
        {
            container.Background(bgColor).Padding(12).Column(col =>
            {
                col.Item().Text(title)
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(8).Text(skills ?? "None")
                    .FontSize(11);
            });
        }

        private void ComposeAISummary(IContainer container, string? aiExplanation)
        {
            container.Background(Colors.Yellow.Lighten4).Padding(12).Column(col =>
            {
                col.Item().Text("AI Summary / Verdict")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);

                col.Item().PaddingTop(8).Text(aiExplanation ?? "No AI analysis available.")
                    .FontSize(11)
                    .LineHeight(1.4f);
            });
        }

        private void ComposeFooter(IContainer container, int pageNumber)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text($"Generated by AICES - {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);

                row.ConstantItem(100).AlignRight().Text($"Page {pageNumber}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        }

        #endregion

        public async Task<ServiceResponse> GetExecutiveSummaryAsync()
        {
            try
            {
                // 1. Total Companies
                var totalCompanies = await _reportRepository.GetTotalActiveCompaniesAsync();

                // 2. Active Companies (có ít nhất 1 job published hoặc có subscription active)
                var activeCompanies = await _reportRepository.GetActiveCompaniesWithJobsOrSubscriptionsAsync();

                // 3. Total Jobs (all active jobs)
                var totalJobs = await _reportRepository.GetTotalActiveJobsAsync();

                // 4. AI Processed Resumes (resumes with score)
                var aiProcessedResumes = await _reportRepository.GetAiProcessedResumesCountAsync();

                // 5. Total Revenue (sum of successful payments via transactions)
                var totalRevenue = await _reportRepository.GetTotalRevenueFromPaidPaymentsAsync();

                // 6. Company Retention Rate (companies that have renewed - have more than 1 subscription in history)
                var companiesWithSubscriptions = await _reportRepository.GetCompaniesWithSubscriptionsCountAsync();

                var companiesWithMultipleSubscriptions = await _reportRepository.GetCompaniesWithMultipleSubscriptionsCountAsync();

                decimal retentionRate = companiesWithSubscriptions > 0 
                    ? Math.Round((decimal)companiesWithMultipleSubscriptions / companiesWithSubscriptions, 2)
                    : 0;

                var summary = new ExecutiveSummaryResponse
                {
                    TotalCompanies = totalCompanies,
                    ActiveCompanies = activeCompanies,
                    TotalJobs = totalJobs,
                    AiProcessedResumes = aiProcessedResumes,
                    TotalRevenue = totalRevenue,
                    CompanyRetentionRate = retentionRate
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Executive summary retrieved successfully.",
                    Data = summary
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve executive summary: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetCompaniesOverviewAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                // 1. Total Companies
                var totalCompanies = await _reportRepository.GetTotalActiveCompaniesCountAsync();

                // 2. Active Companies (status = Approved)
                var activeCompanies = await _reportRepository.GetCompaniesByStatusAsync(CompanyStatusEnum.Approved);

                // 3. Inactive Companies (IsActive = true but not Approved)
                var inactiveCompanies = totalCompanies - activeCompanies;

                // 4. New Companies This Month
                var newCompaniesThisMonth = await _reportRepository.GetNewCompaniesThisMonthAsync(firstDayOfMonth);

                // 5. Companies with Active Subscription
                var companiesWithActiveSubscription = await _reportRepository.GetCompaniesWithActiveSubscriptionAsync();

                // 6. Companies with Expired Subscription (có subscription nhưng không có active)
                var companiesWithExpiredSubscription = await _reportRepository.GetCompaniesWithExpiredSubscriptionAsync();

                // 7. Companies without any Subscription
                var companiesWithoutSubscription = await _reportRepository.GetCompaniesWithoutSubscriptionAsync();

                // 8. Verification Status Breakdown
                var verifiedCompanies = await _reportRepository.GetCompaniesByStatusAsync(CompanyStatusEnum.Approved);

                var pendingCompanies = await _reportRepository.GetCompaniesByStatusAsync(CompanyStatusEnum.Pending);

                var rejectedCompanies = await _reportRepository.GetCompaniesByStatusAsync(CompanyStatusEnum.Rejected);

                var overview = new CompanyOverviewResponse
                {
                    TotalCompanies = totalCompanies,
                    ActiveCompanies = activeCompanies,
                    InactiveCompanies = inactiveCompanies,
                    NewCompaniesThisMonth = newCompaniesThisMonth,
                    SubscriptionBreakdown = new CompanyBySubscriptionStatus
                    {
                        WithActiveSubscription = companiesWithActiveSubscription,
                        WithExpiredSubscription = companiesWithExpiredSubscription,
                        WithoutSubscription = companiesWithoutSubscription
                    },
                    VerificationBreakdown = new CompanyByVerificationStatus
                    {
                        Verified = verifiedCompanies,
                        Pending = pendingCompanies,
                        Rejected = rejectedCompanies
                    }
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company overview retrieved successfully.",
                    Data = overview
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve company overview: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetCompaniesUsageAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var sevenDaysAgo = now.AddDays(-7);
                var thirtyDaysAgo = now.AddDays(-30);

                // Total active companies (approved)
                var totalCompanies = await _reportRepository.GetCompaniesByStatusAsync(CompanyStatusEnum.Approved);

                // 1. Active Companies: Có ít nhất 1 job hoặc 1 resume
                var activeCompanies = await _reportRepository.GetActiveCompaniesWithContentAsync();

                // 2. Registered Only: Đăng ký nhưng chưa tạo job hoặc resume nào
                var registeredOnly = totalCompanies - activeCompanies;

                // 3. Frequent Companies: Có hoạt động trong 7 ngày gần đây
                var frequentCompanies = await _reportRepository.GetFrequentCompaniesAsync(sevenDaysAgo);

                // KPIs
                // 4. Active Rate: % công ty active / tổng công ty
                decimal activeRate = totalCompanies > 0 
                    ? Math.Round((decimal)activeCompanies / totalCompanies, 2)
                    : 0;

                // 5. AI Usage Rate: % công ty có dùng AI screening (có resume được score)
                var companiesUsingAI = await _reportRepository.GetCompaniesUsingAIAsync();

                decimal aiUsageRate = totalCompanies > 0 
                    ? Math.Round((decimal)companiesUsingAI / totalCompanies, 2)
                    : 0;

                // 6. Returning Rate: % công ty có hoạt động trong 30 ngày gần đây
                var returningCompanies = await _reportRepository.GetReturningCompaniesAsync(thirtyDaysAgo);

                decimal returningRate = totalCompanies > 0 
                    ? Math.Round((decimal)returningCompanies / totalCompanies, 2)
                    : 0;

                var usage = new CompanyUsageResponse
                {
                    RegisteredOnly = registeredOnly,
                    ActiveCompanies = activeCompanies,
                    FrequentCompanies = frequentCompanies,
                    Kpis = new CompanyUsageKpis
                    {
                        ActiveRate = activeRate,
                        AiUsageRate = aiUsageRate,
                        ReturningRate = returningRate
                    }
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Company usage retrieved successfully.",
                    Data = usage
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve company usage: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetJobsStatisticsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                // 1. Total Jobs (all active)
                var totalJobs = await _reportRepository.GetTotalActiveJobsCountAsync();

                // 2. Active Jobs (Published status)
                var activeJobs = await _reportRepository.GetActiveJobsByStatusAsync(JobStatusEnum.Published);

                // 3. Pending Jobs (waiting approval)
                var pendingJobs = await _reportRepository.GetActiveJobsByStatusAsync(JobStatusEnum.Pending);

                // 4. Rejected Jobs
                var rejectedJobs = await _reportRepository.GetActiveJobsByStatusAsync(JobStatusEnum.Rejected);

                // 5. Archived Jobs
                var archivedJobs = await _reportRepository.GetActiveJobsByStatusAsync(JobStatusEnum.Archived);

                // 6. New Jobs This Month
                var newJobsThisMonth = await _reportRepository.GetNewJobsThisMonthAsync(firstDayOfMonth);

                // 7. Average Applications Per Job
                var totalApplications = await _reportRepository.GetTotalApplicationsCountAsync();

                var jobsWithApplications = await _reportRepository.GetJobsWithApplicationsCountAsync();

                decimal avgApplicationsPerJob = jobsWithApplications > 0
                    ? Math.Round((decimal)totalApplications / jobsWithApplications, 2)
                    : 0;

                // 8. Top Categories (top 5)
                var topCategoriesData = await _reportRepository.GetTopCategoriesByJobCountAsync(5);
                var topCategories = topCategoriesData.Select(tc => new TopCategoryJob
                {
                    CategoryId = tc.CategoryId,
                    CategoryName = tc.CategoryName,
                    JobCount = tc.JobCount
                }).ToList();

                var statistics = new JobStatisticsResponse
                {
                    TotalJobs = totalJobs,
                    ActiveJobs = activeJobs,
                    DraftJobs = pendingJobs,
                    ClosedJobs = archivedJobs,
                    NewJobsThisMonth = newJobsThisMonth,
                    AverageApplicationsPerJob = avgApplicationsPerJob,
                    StatusBreakdown = new JobsByStatusBreakdown
                    {
                        Published = activeJobs,
                        Draft = pendingJobs,
                        Closed = archivedJobs
                    },
                    TopCategories = topCategories
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job statistics retrieved successfully.",
                    Data = statistics
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve job statistics: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetJobsEffectivenessAsync()
        {
            try
            {
                // 1. Average Resumes Per Job
                var totalResumes = await _reportRepository.GetTotalResumesCountAsync();

                var totalJobs = await _reportRepository.GetJobsWithApplicationsCountAsync();

                decimal avgResumesPerJob = totalJobs > 0
                    ? Math.Round((decimal)totalResumes / totalJobs, 2)
                    : 0;

                // 2. Qualified Rate: Tỉ lệ CV có AI Score > 75
                var totalApplicationsWithScore = await _reportRepository.GetTotalApplicationsWithScoreAsync();

                var qualifiedApplications = await _reportRepository.GetQualifiedApplicationsCountAsync();

                decimal qualifiedRate = totalApplicationsWithScore > 0
                    ? Math.Round((decimal)qualifiedApplications / totalApplicationsWithScore, 2)
                    : 0;

                // 3. Success Hiring Rate: Tỉ lệ job có ít nhất 1 ứng viên được chấp nhận
                var totalJobsPublished = await _reportRepository.GetTotalPublishedJobsCountAsync();

                var successfulJobs = await _reportRepository.GetSuccessfulJobsCountAsync();

                decimal successHiringRate = totalJobsPublished > 0
                    ? Math.Round((decimal)successfulJobs / totalJobsPublished, 2)
                    : 0;

                var effectiveness = new JobEffectivenessResponse
                {
                    AverageResumesPerJob = avgResumesPerJob,
                    QualifiedRate = qualifiedRate,
                    SuccessHiringRate = successHiringRate
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Job effectiveness retrieved successfully.",
                    Data = effectiveness
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve job effectiveness: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetAiParsingQualityAsync()
        {
            try
            {
                // 1. Total Resumes
                var totalResumes = await _reportRepository.GetTotalResumesForParsingAsync();

                // 2. Successful Parsing (Status = Completed)
                var successfulParsing = await _reportRepository.GetSuccessfulParsingCountAsync();

                // 3. Failed Parsing (all other statuses except Completed and Pending)
                var failedParsing = await _reportRepository.GetFailedParsingCountAsync();

                // 4. Success Rate
                decimal successRate = totalResumes > 0
                    ? Math.Round((decimal)successfulParsing / totalResumes, 2)
                    : 0;

                // 5. Average Processing Time (from ResumeApplications)
                var avgProcessingTime = await _reportRepository.GetAverageProcessingTimeFromApplicationsAsync();

                // 6. Common Errors - Group by ResumeStatus
                var commonErrors = await _reportRepository.GetCommonParsingErrorsAsync(5);

                var parsingQuality = new AiParsingQualityResponse
                {
                    SuccessRate = successRate,
                    TotalResumes = totalResumes,
                    SuccessfulParsing = successfulParsing,
                    FailedParsing = failedParsing,
                    AverageProcessingTimeMs = Math.Round(avgProcessingTime, 0),
                    CommonErrors = commonErrors
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "AI parsing quality retrieved successfully.",
                    Data = parsingQuality
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve AI parsing quality: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetAiScoringDistributionAsync()
        {
            try
            {
                // 1. Total Applications with Score
                var totalScored = await _reportRepository.GetTotalScoredApplicationsAsync();

                // 2. Get all scores for distribution calculation
                var scores = await _reportRepository.GetAllScoresAsync();

                // 3. Score Distribution
                var highScores = scores.Count(s => s > 75);
                var mediumScores = scores.Count(s => s >= 50 && s <= 75);
                var lowScores = scores.Count(s => s < 50);

                decimal highPct = totalScored > 0 ? Math.Round((decimal)highScores / totalScored, 2) : 0;
                decimal mediumPct = totalScored > 0 ? Math.Round((decimal)mediumScores / totalScored, 2) : 0;
                decimal lowPct = totalScored > 0 ? Math.Round((decimal)lowScores / totalScored, 2) : 0;

                // 4. Success Rate (applications that got scored successfully)
                var totalApplications = await _reportRepository.GetTotalApplicationsForScoringAsync();

                decimal successRate = totalApplications > 0
                    ? Math.Round((decimal)totalScored / totalApplications, 2)
                    : 0;

                // 5. Average Processing Time
                var avgProcessingTime = await _reportRepository.GetAverageProcessingTimeForScoringAsync();

                // 6. Common Errors from ErrorMessage
                var commonErrors = await _reportRepository.GetCommonScoringErrorsAsync(10);

                // 7. Statistics
                var avgScore = scores.Any() ? Math.Round((decimal)scores.Average(), 2) : 0;
                var sortedScores = scores.OrderBy(s => s).ToList();
                var medianScore = sortedScores.Any() 
                    ? sortedScores.Count % 2 == 0 
                        ? Math.Round((sortedScores[sortedScores.Count / 2 - 1] + sortedScores[sortedScores.Count / 2]) / 2, 2)
                        : sortedScores[sortedScores.Count / 2]
                    : 0;

                var scoringDistribution = new AiScoringDistributionResponse
                {
                    SuccessRate = successRate,
                    ScoreDistribution = new ScoreDistribution
                    {
                        High = highPct,
                        Medium = mediumPct,
                        Low = lowPct
                    },
                    AverageProcessingTimeMs = Math.Round(avgProcessingTime, 0),
                    CommonErrors = commonErrors,
                    Statistics = new ScoringStatistics
                    {
                        TotalScored = totalScored,
                        AverageScore = avgScore,
                        MedianScore = medianScore
                    }
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "AI scoring distribution retrieved successfully.",
                    Data = scoringDistribution
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve AI scoring distribution: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetSubscriptionRevenueAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. Total Active Companies
                var totalActiveCompanies = await _reportRepository.GetTotalActiveCompaniesForSubscriptionAsync();

                // 2. Paid Companies (có active subscription)
                var paidCompanies = await _reportRepository.GetPaidCompaniesCountAsync();

                // 3. Free Companies (không có active subscription)
                var freeCompanies = totalActiveCompanies - paidCompanies;

                // 4. Monthly Revenue (transactions trong tháng này với payment status = Paid)
                var monthlyRevenue = await _reportRepository.GetMonthlyRevenueAsync(now.Year, now.Month);

                // 5. Renewal Rate (công ty có > 1 subscription / tổng công ty có subscription)
                var companiesWithSubscriptions = await _reportRepository.GetCompaniesWithSubscriptionsForRevenueAsync();

                var companiesWithMultipleSubscriptions = await _reportRepository.GetCompaniesWithMultipleSubscriptionsForRevenueAsync();

                decimal renewalRate = companiesWithSubscriptions > 0
                    ? Math.Round((decimal)companiesWithMultipleSubscriptions / companiesWithSubscriptions, 2)
                    : 0;

                // 6. Popular Plan and Plan Statistics
                var planStatistics = await _reportRepository.GetPlanStatisticsAsync();

                var popularPlan = planStatistics.OrderByDescending(p => p.CompanyCount).FirstOrDefault()?.PlanName ?? "N/A";

                // 8. Total Revenue (all time)
                var totalRevenue = await _reportRepository.GetTotalRevenueAsync();

                decimal avgRevenuePerCompany = paidCompanies > 0
                    ? Math.Round(totalRevenue / paidCompanies, 2)
                    : 0;

                var subscriptionRevenue = new SubscriptionRevenueResponse
                {
                    FreeCompanies = freeCompanies,
                    PaidCompanies = paidCompanies,
                    MonthlyRevenue = monthlyRevenue,
                    RenewalRate = renewalRate,
                    PopularPlan = popularPlan,
                    Breakdown = new SubscriptionBreakdown
                    {
                        PlanStatistics = planStatistics,
                        TotalRevenue = totalRevenue,
                        AverageRevenuePerCompany = avgRevenuePerCompany
                    }
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Subscription revenue retrieved successfully.",
                    Data = subscriptionRevenue
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"Failed to retrieve subscription revenue: {ex.Message}"
                };
            }
        }

        #region Export Methods for System Reports

        public async Task<ServiceResponse> ExportExecutiveSummaryToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetExecutiveSummaryAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as ExecutiveSummaryResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve executive summary data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Executive Summary");

                // Title
                ws.Cells[1, 1].Value = "Executive Summary Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Report Date
                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var reportData = new Dictionary<string, object>
                {
                    { "Total Companies", data.TotalCompanies },
                    { "Active Companies", data.ActiveCompanies },
                    { "Total Jobs", data.TotalJobs },
                    { "AI Processed Resumes", data.AiProcessedResumes },
                    { "Total Revenue", data.TotalRevenue },
                    { "Company Retention Rate", $"{data.CompanyRetentionRate:P2}" }
                };

                // Headers
                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                row++;
                foreach (var item in reportData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Executive_Summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportExecutiveSummaryToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetExecutiveSummaryAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as ExecutiveSummaryResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve executive summary data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Executive Summary Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("Metric").Bold();
                                        header.Cell().Element(CellStyle).Text("Value").Bold();
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Companies");
                                    table.Cell().Element(CellStyle).Text(data.TotalCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Active Companies");
                                    table.Cell().Element(CellStyle).Text(data.ActiveCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Total Jobs");
                                    table.Cell().Element(CellStyle).Text(data.TotalJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("AI Processed Resumes");
                                    table.Cell().Element(CellStyle).Text(data.AiProcessedResumes.ToString());

                                    table.Cell().Element(CellStyle).Text("Total Revenue");
                                    table.Cell().Element(CellStyle).Text($"${data.TotalRevenue:N2}");

                                    table.Cell().Element(CellStyle).Text("Company Retention Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.CompanyRetentionRate:P2}");
                                });
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Executive_Summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportCompaniesOverviewToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetCompaniesOverviewAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as CompanyOverviewResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve companies overview data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Companies Overview");

                // Title
                ws.Cells[1, 1].Value = "Companies Overview Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var mainData = new Dictionary<string, object>
                {
                    { "Total Companies", data.TotalCompanies },
                    { "Active Companies", data.ActiveCompanies },
                    { "Inactive Companies", data.InactiveCompanies },
                    { "New Companies This Month", data.NewCompaniesThisMonth }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in mainData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Subscription Breakdown";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var subscriptionData = new Dictionary<string, int>
                {
                    { "With Active Subscription", data.SubscriptionBreakdown.WithActiveSubscription },
                    { "With Expired Subscription", data.SubscriptionBreakdown.WithExpiredSubscription },
                    { "Without Subscription", data.SubscriptionBreakdown.WithoutSubscription }
                };

                foreach (var item in subscriptionData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Verification Breakdown";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var verificationData = new Dictionary<string, int>
                {
                    { "Verified", data.VerificationBreakdown.Verified },
                    { "Pending", data.VerificationBreakdown.Pending },
                    { "Rejected", data.VerificationBreakdown.Rejected }
                };

                foreach (var item in verificationData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Companies_Overview_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportCompaniesOverviewToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetCompaniesOverviewAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as CompanyOverviewResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve companies overview data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Companies Overview Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Overview").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Companies");
                                    table.Cell().Element(CellStyle).Text(data.TotalCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Active Companies");
                                    table.Cell().Element(CellStyle).Text(data.ActiveCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Inactive Companies");
                                    table.Cell().Element(CellStyle).Text(data.InactiveCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("New Companies This Month");
                                    table.Cell().Element(CellStyle).Text(data.NewCompaniesThisMonth.ToString());
                                });

                                column.Item().PaddingTop(20).Text("Subscription Breakdown").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("With Active Subscription");
                                    table.Cell().Element(CellStyle).Text(data.SubscriptionBreakdown.WithActiveSubscription.ToString());

                                    table.Cell().Element(CellStyle).Text("With Expired Subscription");
                                    table.Cell().Element(CellStyle).Text(data.SubscriptionBreakdown.WithExpiredSubscription.ToString());

                                    table.Cell().Element(CellStyle).Text("Without Subscription");
                                    table.Cell().Element(CellStyle).Text(data.SubscriptionBreakdown.WithoutSubscription.ToString());
                                });

                                column.Item().PaddingTop(20).Text("Verification Breakdown").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Verified");
                                    table.Cell().Element(CellStyle).Text(data.VerificationBreakdown.Verified.ToString());

                                    table.Cell().Element(CellStyle).Text("Pending");
                                    table.Cell().Element(CellStyle).Text(data.VerificationBreakdown.Pending.ToString());

                                    table.Cell().Element(CellStyle).Text("Rejected");
                                    table.Cell().Element(CellStyle).Text(data.VerificationBreakdown.Rejected.ToString());
                                });
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Companies_Overview_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportCompaniesUsageToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetCompaniesUsageAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as CompanyUsageResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve companies usage data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Companies Usage");

                ws.Cells[1, 1].Value = "Companies Usage Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var usageData = new Dictionary<string, object>
                {
                    { "Registered Only", data.RegisteredOnly },
                    { "Active Companies", data.ActiveCompanies },
                    { "Frequent Companies", data.FrequentCompanies }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in usageData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "KPIs";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var kpiData = new Dictionary<string, string>
                {
                    { "Active Rate", $"{data.Kpis.ActiveRate:P2}" },
                    { "AI Usage Rate", $"{data.Kpis.AiUsageRate:P2}" },
                    { "Returning Rate", $"{data.Kpis.ReturningRate:P2}" }
                };

                foreach (var item in kpiData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Companies_Usage_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportCompaniesUsageToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetCompaniesUsageAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as CompanyUsageResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve companies usage data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Companies Usage Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Usage Statistics").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Registered Only");
                                    table.Cell().Element(CellStyle).Text(data.RegisteredOnly.ToString());

                                    table.Cell().Element(CellStyle).Text("Active Companies");
                                    table.Cell().Element(CellStyle).Text(data.ActiveCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Frequent Companies");
                                    table.Cell().Element(CellStyle).Text(data.FrequentCompanies.ToString());
                                });

                                column.Item().PaddingTop(20).Text("Key Performance Indicators").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Active Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.Kpis.ActiveRate:P2}");

                                    table.Cell().Element(CellStyle).Text("AI Usage Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.Kpis.AiUsageRate:P2}");

                                    table.Cell().Element(CellStyle).Text("Returning Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.Kpis.ReturningRate:P2}");
                                });
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Companies_Usage_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportJobsStatisticsToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetJobsStatisticsAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as JobStatisticsResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve jobs statistics data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Jobs Statistics");

                ws.Cells[1, 1].Value = "Jobs Statistics Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var mainData = new Dictionary<string, object>
                {
                    { "Total Jobs", data.TotalJobs },
                    { "Active Jobs", data.ActiveJobs },
                    { "Draft Jobs", data.DraftJobs },
                    { "Closed Jobs", data.ClosedJobs },
                    { "New Jobs This Month", data.NewJobsThisMonth },
                    { "Average Applications Per Job", data.AverageApplicationsPerJob }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in mainData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Status Breakdown";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var statusData = new Dictionary<string, int>
                {
                    { "Published", data.StatusBreakdown.Published },
                    { "Draft", data.StatusBreakdown.Draft },
                    { "Closed", data.StatusBreakdown.Closed }
                };

                foreach (var item in statusData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                if (data.TopCategories != null && data.TopCategories.Any())
                {
                    row += 2;
                    ws.Cells[row, 1].Value = "Top Categories";
                    ws.Cells[row, 1, row, 2].Merge = true;
                    ws.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    ws.Cells[row, 1].Value = "Category Name";
                    ws.Cells[row, 2].Value = "Job Count";
                    using (var range = ws.Cells[row, 1, row, 2])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                        range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }
                    row++;

                    foreach (var category in data.TopCategories)
                    {
                        ws.Cells[row, 1].Value = category.CategoryName;
                        ws.Cells[row, 2].Value = category.JobCount;
                        row++;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Jobs_Statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportJobsStatisticsToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetJobsStatisticsAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as JobStatisticsResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve jobs statistics data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Jobs Statistics Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Statistics").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Jobs");
                                    table.Cell().Element(CellStyle).Text(data.TotalJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("Active Jobs");
                                    table.Cell().Element(CellStyle).Text(data.ActiveJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("Draft Jobs");
                                    table.Cell().Element(CellStyle).Text(data.DraftJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("Closed Jobs");
                                    table.Cell().Element(CellStyle).Text(data.ClosedJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("New Jobs This Month");
                                    table.Cell().Element(CellStyle).Text(data.NewJobsThisMonth.ToString());

                                    table.Cell().Element(CellStyle).Text("Average Applications Per Job");
                                    table.Cell().Element(CellStyle).Text(data.AverageApplicationsPerJob.ToString("F2"));
                                });

                                column.Item().PaddingTop(20).Text("Status Breakdown").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Published");
                                    table.Cell().Element(CellStyle).Text(data.StatusBreakdown.Published.ToString());

                                    table.Cell().Element(CellStyle).Text("Draft");
                                    table.Cell().Element(CellStyle).Text(data.StatusBreakdown.Draft.ToString());

                                    table.Cell().Element(CellStyle).Text("Closed");
                                    table.Cell().Element(CellStyle).Text(data.StatusBreakdown.Closed.ToString());
                                });

                                if (data.TopCategories != null && data.TopCategories.Any())
                                {
                                    column.Item().PaddingTop(20).Text("Top Categories").FontSize(14).Bold();
                                    column.Item().PaddingTop(10).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Category Name").Bold();
                                            header.Cell().Element(CellStyle).Text("Job Count").Bold();
                                        });

                                        foreach (var category in data.TopCategories)
                                        {
                                            table.Cell().Element(CellStyle).Text(category.CategoryName);
                                            table.Cell().Element(CellStyle).Text(category.JobCount.ToString());
                                        }
                                    });
                                }
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Jobs_Statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportJobsEffectivenessToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetJobsEffectivenessAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as JobEffectivenessResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve jobs effectiveness data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Jobs Effectiveness");

                ws.Cells[1, 1].Value = "Jobs Effectiveness Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var effectivenessData = new Dictionary<string, string>
                {
                    { "Average Resumes Per Job", data.AverageResumesPerJob.ToString("F2") },
                    { "Qualified Rate", $"{data.QualifiedRate:P2}" },
                    { "Success Hiring Rate", $"{data.SuccessHiringRate:P2}" }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in effectivenessData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Jobs_Effectiveness_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportJobsEffectivenessToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetJobsEffectivenessAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as JobEffectivenessResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve jobs effectiveness data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Jobs Effectiveness Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Average Resumes Per Job");
                                    table.Cell().Element(CellStyle).Text(data.AverageResumesPerJob.ToString("F2"));

                                    table.Cell().Element(CellStyle).Text("Qualified Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.QualifiedRate:P2}");

                                    table.Cell().Element(CellStyle).Text("Success Hiring Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.SuccessHiringRate:P2}");
                                });
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Jobs_Effectiveness_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportAiParsingQualityToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetAiParsingQualityAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as AiParsingQualityResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve AI parsing quality data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("AI Parsing Quality");

                ws.Cells[1, 1].Value = "AI Parsing Quality Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var mainData = new Dictionary<string, object>
                {
                    { "Total Resumes", data.TotalResumes },
                    { "Successful Parsing", data.SuccessfulParsing },
                    { "Failed Parsing", data.FailedParsing },
                    { "Success Rate", $"{data.SuccessRate:P2}" },
                    { "Average Processing Time (ms)", data.AverageProcessingTimeMs.ToString("F2") }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in mainData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                if (data.CommonErrors != null && data.CommonErrors.Any())
                {
                    row += 2;
                    ws.Cells[row, 1].Value = "Common Errors";
                    ws.Cells[row, 1, row, 3].Merge = true;
                    ws.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    ws.Cells[row, 1].Value = "Error Type";
                    ws.Cells[row, 2].Value = "Count";
                    ws.Cells[row, 3].Value = "Percentage";
                    using (var range = ws.Cells[row, 1, row, 3])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                        range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }
                    row++;

                    foreach (var error in data.CommonErrors)
                    {
                        ws.Cells[row, 1].Value = error.ErrorType;
                        ws.Cells[row, 2].Value = error.Count;
                        ws.Cells[row, 3].Value = $"{error.Percentage:P2}";
                        row++;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, data.CommonErrors != null && data.CommonErrors.Any() ? 3 : 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"AI_Parsing_Quality_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportAiParsingQualityToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetAiParsingQualityAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as AiParsingQualityResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve AI parsing quality data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "AI Parsing Quality Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Quality Metrics").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Resumes");
                                    table.Cell().Element(CellStyle).Text(data.TotalResumes.ToString());

                                    table.Cell().Element(CellStyle).Text("Successful Parsing");
                                    table.Cell().Element(CellStyle).Text(data.SuccessfulParsing.ToString());

                                    table.Cell().Element(CellStyle).Text("Failed Parsing");
                                    table.Cell().Element(CellStyle).Text(data.FailedParsing.ToString());

                                    table.Cell().Element(CellStyle).Text("Success Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.SuccessRate:P2}");

                                    table.Cell().Element(CellStyle).Text("Average Processing Time (ms)");
                                    table.Cell().Element(CellStyle).Text(data.AverageProcessingTimeMs.ToString("F2"));
                                });

                                if (data.CommonErrors != null && data.CommonErrors.Any())
                                {
                                    column.Item().PaddingTop(20).Text("Common Errors").FontSize(14).Bold();
                                    column.Item().PaddingTop(10).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Error Type").Bold();
                                            header.Cell().Element(CellStyle).Text("Count").Bold();
                                            header.Cell().Element(CellStyle).Text("Percentage").Bold();
                                        });

                                        foreach (var error in data.CommonErrors)
                                        {
                                            table.Cell().Element(CellStyle).Text(error.ErrorType);
                                            table.Cell().Element(CellStyle).Text(error.Count.ToString());
                                            table.Cell().Element(CellStyle).Text($"{error.Percentage:P2}");
                                        }
                                    });
                                }
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"AI_Parsing_Quality_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportAiScoringDistributionToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetAiScoringDistributionAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as AiScoringDistributionResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve AI scoring distribution data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("AI Scoring Distribution");

                ws.Cells[1, 1].Value = "AI Scoring Distribution Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var mainData = new Dictionary<string, object>
                {
                    { "Success Rate", $"{data.SuccessRate:P2}" },
                    { "Average Processing Time (ms)", data.AverageProcessingTimeMs.ToString("F2") }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in mainData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Score Distribution";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var distributionData = new Dictionary<string, string>
                {
                    { "High (>75)", $"{data.ScoreDistribution.High:P2}" },
                    { "Medium (50-75)", $"{data.ScoreDistribution.Medium:P2}" },
                    { "Low (<50)", $"{data.ScoreDistribution.Low:P2}" }
                };

                foreach (var item in distributionData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Statistics";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var statsData = new Dictionary<string, object>
                {
                    { "Total Scored", data.Statistics.TotalScored },
                    { "Average Score", data.Statistics.AverageScore.ToString("F2") },
                    { "Median Score", data.Statistics.MedianScore.ToString("F2") }
                };

                foreach (var item in statsData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                if (data.CommonErrors != null && data.CommonErrors.Any())
                {
                    row += 2;
                    ws.Cells[row, 1].Value = "Common Errors";
                    ws.Cells[row, 1, row, 1].Merge = true;
                    ws.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    foreach (var error in data.CommonErrors)
                    {
                        ws.Cells[row, 1].Value = error;
                        row++;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                using (var range = ws.Cells[4, 1, row - 1, 2])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"AI_Scoring_Distribution_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportAiScoringDistributionToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetAiScoringDistributionAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as AiScoringDistributionResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve AI scoring distribution data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "AI Scoring Distribution Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Overview").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Success Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.SuccessRate:P2}");

                                    table.Cell().Element(CellStyle).Text("Average Processing Time (ms)");
                                    table.Cell().Element(CellStyle).Text(data.AverageProcessingTimeMs.ToString("F2"));
                                });

                                column.Item().PaddingTop(20).Text("Score Distribution").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("High (>75)");
                                    table.Cell().Element(CellStyle).Text($"{data.ScoreDistribution.High:P2}");

                                    table.Cell().Element(CellStyle).Text("Medium (50-75)");
                                    table.Cell().Element(CellStyle).Text($"{data.ScoreDistribution.Medium:P2}");

                                    table.Cell().Element(CellStyle).Text("Low (<50)");
                                    table.Cell().Element(CellStyle).Text($"{data.ScoreDistribution.Low:P2}");
                                });

                                column.Item().PaddingTop(20).Text("Statistics").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Scored");
                                    table.Cell().Element(CellStyle).Text(data.Statistics.TotalScored.ToString());

                                    table.Cell().Element(CellStyle).Text("Average Score");
                                    table.Cell().Element(CellStyle).Text(data.Statistics.AverageScore.ToString("F2"));

                                    table.Cell().Element(CellStyle).Text("Median Score");
                                    table.Cell().Element(CellStyle).Text(data.Statistics.MedianScore.ToString("F2"));
                                });

                                if (data.CommonErrors != null && data.CommonErrors.Any())
                                {
                                    column.Item().PaddingTop(20).Text("Common Errors").FontSize(14).Bold();
                                    column.Item().PaddingTop(10).Column(column =>
                                    {
                                        foreach (var error in data.CommonErrors)
                                        {
                                            column.Item().PaddingBottom(5).Text($"• {error}").FontSize(10);
                                        }
                                    });
                                }
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"AI_Scoring_Distribution_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportSubscriptionRevenueToExcelAsync()
        {
            try
            {
                var serviceResponse = await GetSubscriptionRevenueAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as SubscriptionRevenueResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve subscription revenue data."
                    };
                }

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("Subscription Revenue");

                ws.Cells[1, 1].Value = "Subscription Revenue Report";
                ws.Cells[1, 1, 1, 2].Merge = true;
                using (var range = ws.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size = 16;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                ws.Cells[2, 1].Value = "Generated Date:";
                ws.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                ws.Cells[2, 1].Style.Font.Bold = true;

                int row = 4;
                var mainData = new Dictionary<string, object>
                {
                    { "Free Companies", data.FreeCompanies },
                    { "Paid Companies", data.PaidCompanies },
                    { "Monthly Revenue", $"${data.MonthlyRevenue:N2}" },
                    { "Renewal Rate", $"{data.RenewalRate:P2}" },
                    { "Popular Plan", data.PopularPlan }
                };

                ws.Cells[row, 1].Value = "Metric";
                ws.Cells[row, 2].Value = "Value";
                using (var range = ws.Cells[row, 1, row, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                row++;
                foreach (var item in mainData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                row += 2;
                ws.Cells[row, 1].Value = "Breakdown";
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                var breakdownData = new Dictionary<string, string>
                {
                    { "Total Revenue", $"${data.Breakdown.TotalRevenue:N2}" },
                    { "Average Revenue Per Company", $"${data.Breakdown.AverageRevenuePerCompany:N2}" }
                };

                foreach (var item in breakdownData)
                {
                    ws.Cells[row, 1].Value = item.Key;
                    ws.Cells[row, 2].Value = item.Value;
                    row++;
                }

                if (data.Breakdown.PlanStatistics != null && data.Breakdown.PlanStatistics.Any())
                {
                    row += 2;
                    ws.Cells[row, 1].Value = "Plan Statistics";
                    ws.Cells[row, 1, row, 4].Merge = true;
                    ws.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    ws.Cells[row, 1].Value = "Plan Name";
                    ws.Cells[row, 2].Value = "Company Count";
                    ws.Cells[row, 3].Value = "Revenue";
                    using (var range = ws.Cells[row, 1, row, 3])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(70, 180, 77));
                        range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }
                    row++;

                    foreach (var plan in data.Breakdown.PlanStatistics)
                    {
                        ws.Cells[row, 1].Value = plan.PlanName;
                        ws.Cells[row, 2].Value = plan.CompanyCount;
                        ws.Cells[row, 3].Value = $"${plan.Revenue:N2}";
                        row++;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                var lastCol = data.Breakdown.PlanStatistics != null && data.Breakdown.PlanStatistics.Any() ? 3 : 2;
                using (var range = ws.Cells[4, 1, row - 1, lastCol])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                var fileBytes = package.GetAsByteArray();
                var fileName = $"Subscription_Revenue_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

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
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to Excel: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> ExportSubscriptionRevenueToPdfAsync()
        {
            try
            {
                var serviceResponse = await GetSubscriptionRevenueAsync();
                if (serviceResponse.Status != SRStatus.Success || serviceResponse.Data == null)
                {
                    return serviceResponse;
                }

                var data = serviceResponse.Data as SubscriptionRevenueResponse;
                if (data == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve subscription revenue data."
                    };
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header().Element(c => ComposeReportHeader(c, "Subscription Revenue Report"));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                column.Item().PaddingBottom(10).Text("Generated Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(10).FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(20).Text("Overview").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Free Companies");
                                    table.Cell().Element(CellStyle).Text(data.FreeCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Paid Companies");
                                    table.Cell().Element(CellStyle).Text(data.PaidCompanies.ToString());

                                    table.Cell().Element(CellStyle).Text("Monthly Revenue");
                                    table.Cell().Element(CellStyle).Text($"${data.MonthlyRevenue:N2}");

                                    table.Cell().Element(CellStyle).Text("Renewal Rate");
                                    table.Cell().Element(CellStyle).Text($"{data.RenewalRate:P2}");

                                    table.Cell().Element(CellStyle).Text("Popular Plan");
                                    table.Cell().Element(CellStyle).Text(data.PopularPlan);
                                });

                                column.Item().PaddingTop(20).Text("Breakdown").FontSize(14).Bold();
                                column.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Cell().Element(CellStyle).Text("Total Revenue");
                                    table.Cell().Element(CellStyle).Text($"${data.Breakdown.TotalRevenue:N2}");

                                    table.Cell().Element(CellStyle).Text("Average Revenue Per Company");
                                    table.Cell().Element(CellStyle).Text($"${data.Breakdown.AverageRevenuePerCompany:N2}");
                                });

                                if (data.Breakdown.PlanStatistics != null && data.Breakdown.PlanStatistics.Any())
                                {
                                    column.Item().PaddingTop(20).Text("Plan Statistics").FontSize(14).Bold();
                                    column.Item().PaddingTop(10).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Plan Name").Bold();
                                            header.Cell().Element(CellStyle).Text("Company Count").Bold();
                                            header.Cell().Element(CellStyle).Text("Revenue").Bold();
                                        });

                                        foreach (var plan in data.Breakdown.PlanStatistics)
                                        {
                                            table.Cell().Element(CellStyle).Text(plan.PlanName);
                                            table.Cell().Element(CellStyle).Text(plan.CompanyCount.ToString());
                                            table.Cell().Element(CellStyle).Text($"${plan.Revenue:N2}");
                                        }
                                    });
                                }
                            });
                        });

                        page.Footer().Element(c => ComposeReportFooter(c, 1));
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var fileName = $"Subscription_Revenue_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF report generated successfully.",
                    Data = new PdfExportResponse
                    {
                        FileBytes = fileBytes,
                        FileName = fileName,
                        ContentType = "application/pdf"
                    }
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while exporting to PDF: {ex.Message}"
                };
            }
        }

        #endregion

        #region PDF Helper Methods

        private static void ComposeReportHeader(IContainer container, string title)
        {
            container
                .PaddingBottom(10)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(title).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
        }

        private static void ComposeReportFooter(IContainer container, int pageNumber)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text($"Page {pageNumber}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(8)
                .Background(Colors.White);
        }

        #endregion
    }
}
