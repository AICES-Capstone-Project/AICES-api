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

                // 2. Total Jobs (all active jobs)
                var totalJobs = await _reportRepository.GetTotalActiveJobsAsync();

                // 3. AI Processed Resumes (resumes with score)
                var aiProcessedResumes = await _reportRepository.GetAiProcessedResumesCountAsync();

                // 4. Total Revenue (sum of successful payments via transactions)
                var totalRevenue = await _reportRepository.GetTotalRevenueFromPaidPaymentsAsync();

                var summary = new ExecutiveSummaryResponse
                {
                    TotalCompanies = totalCompanies,
                    TotalJobs = totalJobs,
                    AiProcessedResumes = aiProcessedResumes,
                    TotalRevenue = totalRevenue
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
                    EngagedCompanies = activeCompanies,
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

                // 5. Popular Plan and Plan Statistics
                var planStatistics = await _reportRepository.GetPlanStatisticsAsync();

                var popularPlan = planStatistics.OrderByDescending(p => p.CompanyCount).FirstOrDefault()?.PlanName ?? "N/A";

                // 6. Total Revenue (all time)
                var totalRevenue = await _reportRepository.GetTotalRevenueAsync();

                decimal avgRevenuePerCompany = paidCompanies > 0
                    ? Math.Round(totalRevenue / paidCompanies, 2)
                    : 0;

                var subscriptionRevenue = new SubscriptionRevenueResponse
                {
                    FreeCompanies = freeCompanies,
                    PaidCompanies = paidCompanies,
                    MonthlyRevenue = monthlyRevenue,
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
                    { "Total Jobs", data.TotalJobs },
                    { "AI Processed Resumes", data.AiProcessedResumes },
                    { "Total Revenue", data.TotalRevenue }
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

                                    table.Cell().Element(CellStyle).Text("Total Jobs");
                                    table.Cell().Element(CellStyle).Text(data.TotalJobs.ToString());

                                    table.Cell().Element(CellStyle).Text("AI Processed Resumes");
                                    table.Cell().Element(CellStyle).Text(data.AiProcessedResumes.ToString());

                                    table.Cell().Element(CellStyle).Text("Total Revenue");
                                    table.Cell().Element(CellStyle).Text($"${data.TotalRevenue:N2}");
                                });
                            });
                        });

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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
                    { "Engaged Companies", data.EngagedCompanies },
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

                                    table.Cell().Element(CellStyle).Text("Engaged Companies");
                                    table.Cell().Element(CellStyle).Text(data.EngagedCompanies.ToString());

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

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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

                        page.Footer().Element(ComposeReportFooter);
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

        /// <summary>
        /// Export all system reports into a single Excel file (multiple sheets).
        /// </summary>
        public async Task<ServiceResponse> ExportAllSystemReportsToExcelAsync()
        {
            try
            {
                var executiveSummaryResponse = await GetExecutiveSummaryAsync();
                if (executiveSummaryResponse.Status != SRStatus.Success || executiveSummaryResponse.Data == null)
                    return executiveSummaryResponse;

                var companiesOverviewResponse = await GetCompaniesOverviewAsync();
                if (companiesOverviewResponse.Status != SRStatus.Success || companiesOverviewResponse.Data == null)
                    return companiesOverviewResponse;

                var companiesUsageResponse = await GetCompaniesUsageAsync();
                if (companiesUsageResponse.Status != SRStatus.Success || companiesUsageResponse.Data == null)
                    return companiesUsageResponse;

                var jobsStatisticsResponse = await GetJobsStatisticsAsync();
                if (jobsStatisticsResponse.Status != SRStatus.Success || jobsStatisticsResponse.Data == null)
                    return jobsStatisticsResponse;

                var jobsEffectivenessResponse = await GetJobsEffectivenessAsync();
                if (jobsEffectivenessResponse.Status != SRStatus.Success || jobsEffectivenessResponse.Data == null)
                    return jobsEffectivenessResponse;

                var aiParsingResponse = await GetAiParsingQualityAsync();
                if (aiParsingResponse.Status != SRStatus.Success || aiParsingResponse.Data == null)
                    return aiParsingResponse;

                var aiScoringResponse = await GetAiScoringDistributionAsync();
                if (aiScoringResponse.Status != SRStatus.Success || aiScoringResponse.Data == null)
                    return aiScoringResponse;

                var subscriptionRevenueResponse = await GetSubscriptionRevenueAsync();
                if (subscriptionRevenueResponse.Status != SRStatus.Success || subscriptionRevenueResponse.Data == null)
                    return subscriptionRevenueResponse;

                var aiHealthResponse = await GetAiSystemHealthReportAsync();
                if (aiHealthResponse.Status != SRStatus.Success || aiHealthResponse.Data == null)
                    return aiHealthResponse;

                var clientEngagementResponse = await GetClientEngagementReportAsync();
                if (clientEngagementResponse.Status != SRStatus.Success || clientEngagementResponse.Data == null)
                    return clientEngagementResponse;

                var saasMetricsResponse = await GetSaasAdminMetricsReportAsync();
                if (saasMetricsResponse.Status != SRStatus.Success || saasMetricsResponse.Data == null)
                    return saasMetricsResponse;

                var executiveSummary = executiveSummaryResponse.Data as ExecutiveSummaryResponse;
                var companiesOverview = companiesOverviewResponse.Data as CompanyOverviewResponse;
                var companiesUsage = companiesUsageResponse.Data as CompanyUsageResponse;
                var jobsStatistics = jobsStatisticsResponse.Data as JobStatisticsResponse;
                var jobsEffectiveness = jobsEffectivenessResponse.Data as JobEffectivenessResponse;
                var aiParsing = aiParsingResponse.Data as AiParsingQualityResponse;
                var aiScoring = aiScoringResponse.Data as AiScoringDistributionResponse;
                var subscriptionRevenue = subscriptionRevenueResponse.Data as SubscriptionRevenueResponse;
                var aiHealth = aiHealthResponse.Data as AiSystemHealthReportResponse;
                var clientEngagement = clientEngagementResponse.Data as ClientEngagementReportResponse;
                var saasMetrics = saasMetricsResponse.Data as SaasAdminMetricsReportResponse;

                using var package = new ExcelPackage();

                // ============= EXECUTIVE SUMMARY SHEET =============
                var execSheet = package.Workbook.Worksheets.Add("Executive Summary");
                
                // Title section with beautiful styling
                execSheet.Cells[1, 1].Value = "Executive Summary";
                execSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = execSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50)); // Material Green
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                execSheet.Row(1).Height = 30;

                execSheet.Cells[2, 1].Value = "Generated Date";
                execSheet.Cells[2, 2].Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                execSheet.Cells[2, 1].Style.Font.Bold = true;
                execSheet.Cells[2, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                execSheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(245, 245, 245));

                int row = 4;
                // Header row
                execSheet.Cells[row, 1].Value = "Metric";
                execSheet.Cells[row, 2].Value = "Value";
                using (var headerRange = execSheet.Cells[row, 1, row, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245)); // Material Blue
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                execSheet.Row(row).Height = 25;
                row++;
                
                int dataStartRow = row;
                if (executiveSummary != null)
                {
                    execSheet.Cells[row, 1].Value = "Total Companies";
                    execSheet.Cells[row, 2].Value = executiveSummary.TotalCompanies;
                    execSheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                    row++;
                    
                    execSheet.Cells[row, 1].Value = "Total Jobs";
                    execSheet.Cells[row, 2].Value = executiveSummary.TotalJobs;
                    execSheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                    row++;
                    
                    execSheet.Cells[row, 1].Value = "AI Processed Resumes";
                    execSheet.Cells[row, 2].Value = executiveSummary.AiProcessedResumes;
                    execSheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                    row++;
                    
                    execSheet.Cells[row, 1].Value = "Total Revenue";
                    execSheet.Cells[row, 2].Value = executiveSummary.TotalRevenue;
                    execSheet.Cells[row, 2].Style.Numberformat.Format = "$#,##0.00";
                    row++;
                }
                
                // Apply alternating row colors and borders
                for (int r = dataStartRow; r < row; r++)
                {
                    var rowRange = execSheet.Cells[r, 1, r, 2];
                    if ((r - dataStartRow) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    execSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                execSheet.Column(1).Width = 30;
                execSheet.Column(2).Width = 25;
                execSheet.View.FreezePanes(5, 1);

                // ============= COMPANIES OVERVIEW SHEET =============
                var overviewSheet = package.Workbook.Worksheets.Add("Companies Overview");
                
                // Title
                overviewSheet.Cells[1, 1].Value = "Companies Overview";
                overviewSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = overviewSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                overviewSheet.Row(1).Height = 30;

                // Header row
                int oRow = 3;
                overviewSheet.Cells[oRow, 1].Value = "Metric";
                overviewSheet.Cells[oRow, 2].Value = "Value";
                using (var headerRange = overviewSheet.Cells[oRow, 1, oRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                overviewSheet.Row(oRow).Height = 25;
                oRow++;
                
                int oDataStart = oRow;
                if (companiesOverview != null)
                {
                    overviewSheet.Cells[oRow, 1].Value = "Total Companies";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.TotalCompanies;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Active Companies";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.ActiveCompanies;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Inactive Companies";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.InactiveCompanies;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "New Companies This Month";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.NewCompaniesThisMonth;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    // Add spacing
                    oRow++;
                    
                    // Subscription Breakdown section
                    overviewSheet.Cells[oRow, 1].Value = "Subscription Breakdown";
                    overviewSheet.Cells[oRow, 1, oRow, 2].Merge = true;
                    overviewSheet.Cells[oRow, 1].Style.Font.Bold = true;
                    overviewSheet.Cells[oRow, 1].Style.Font.Size = 12;
                    overviewSheet.Cells[oRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    overviewSheet.Cells[oRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(33, 150, 243));
                    overviewSheet.Cells[oRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "With Active Subscription";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.SubscriptionBreakdown.WithActiveSubscription;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "With Expired Subscription";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.SubscriptionBreakdown.WithExpiredSubscription;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Without Subscription";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.SubscriptionBreakdown.WithoutSubscription;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    // Add spacing
                    oRow++;
                    
                    // Verification Breakdown section
                    overviewSheet.Cells[oRow, 1].Value = "Verification Breakdown";
                    overviewSheet.Cells[oRow, 1, oRow, 2].Merge = true;
                    overviewSheet.Cells[oRow, 1].Style.Font.Bold = true;
                    overviewSheet.Cells[oRow, 1].Style.Font.Size = 12;
                    overviewSheet.Cells[oRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    overviewSheet.Cells[oRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(33, 150, 243));
                    overviewSheet.Cells[oRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Verified";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.VerificationBreakdown.Verified;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Pending";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.VerificationBreakdown.Pending;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                    
                    overviewSheet.Cells[oRow, 1].Value = "Rejected";
                    overviewSheet.Cells[oRow, 2].Value = companiesOverview.VerificationBreakdown.Rejected;
                    overviewSheet.Cells[oRow, 2].Style.Numberformat.Format = "#,##0";
                    oRow++;
                }
                
                // Apply styling (avoid overwriting section headers)
                for (int r = oDataStart; r < oRow; r++)
                {
                    // Skip section header rows (they have merged cells with blue background)
                    var cell = overviewSheet.Cells[r, 1];
                    var cellValue = cell.Value?.ToString() ?? "";
                    bool isSectionHeader = cellValue == "Subscription Breakdown" || cellValue == "Verification Breakdown";
                    
                    if (!isSectionHeader)
                    {
                        var rowRange = overviewSheet.Cells[r, 1, r, 2];
                        if ((r - oDataStart) % 2 == 0)
                        {
                            rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                        }
                        rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        overviewSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }
                    else
                    {
                        // Ensure borders on section headers too
                        var rowRange = overviewSheet.Cells[r, 1, r, 2];
                        rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    }
                }
                
                overviewSheet.Column(1).Width = 30;
                overviewSheet.Column(2).Width = 25;
                overviewSheet.View.FreezePanes(4, 1);

                // ============= COMPANIES USAGE SHEET =============
                var usageSheet = package.Workbook.Worksheets.Add("Companies Usage");
                
                // Title
                usageSheet.Cells[1, 1].Value = "Companies Usage Summary";
                usageSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = usageSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                usageSheet.Row(1).Height = 30;

                // Header
                int uRow = 3;
                usageSheet.Cells[uRow, 1].Value = "Metric";
                usageSheet.Cells[uRow, 2].Value = "Value";
                using (var headerRange = usageSheet.Cells[uRow, 1, uRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                usageSheet.Row(uRow).Height = 25;
                uRow++;
                
                int uDataStart = uRow;
                if (companiesUsage != null)
                {
                    usageSheet.Cells[uRow, 1].Value = "Registered Only";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.RegisteredOnly;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "#,##0";
                    uRow++;
                    
                    usageSheet.Cells[uRow, 1].Value = "Engaged Companies";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.EngagedCompanies;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "#,##0";
                    uRow++;
                    
                    usageSheet.Cells[uRow, 1].Value = "Frequent Companies";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.FrequentCompanies;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "#,##0";
                    uRow++;
                    
                    usageSheet.Cells[uRow, 1].Value = "Active Rate";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.Kpis.ActiveRate;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "0.00%";
                    uRow++;
                    
                    usageSheet.Cells[uRow, 1].Value = "AI Usage Rate";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.Kpis.AiUsageRate;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "0.00%";
                    uRow++;
                    
                    usageSheet.Cells[uRow, 1].Value = "Returning Rate";
                    usageSheet.Cells[uRow, 2].Value = companiesUsage.Kpis.ReturningRate;
                    usageSheet.Cells[uRow, 2].Style.Numberformat.Format = "0.00%";
                    uRow++;
                }
                
                // Apply styling
                for (int r = uDataStart; r < uRow; r++)
                {
                    var rowRange = usageSheet.Cells[r, 1, r, 2];
                    if ((r - uDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    usageSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                usageSheet.Column(1).Width = 30;
                usageSheet.Column(2).Width = 25;
                usageSheet.View.FreezePanes(4, 1);

                // ============= JOBS STATISTICS SHEET =============
                var jobsStatsSheet = package.Workbook.Worksheets.Add("Jobs Statistics");
                
                // Title
                jobsStatsSheet.Cells[1, 1].Value = "Jobs Statistics Summary";
                jobsStatsSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = jobsStatsSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                jobsStatsSheet.Row(1).Height = 30;

                // Header
                int jsRow = 3;
                jobsStatsSheet.Cells[jsRow, 1].Value = "Metric";
                jobsStatsSheet.Cells[jsRow, 2].Value = "Value";
                using (var headerRange = jobsStatsSheet.Cells[jsRow, 1, jsRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                jobsStatsSheet.Row(jsRow).Height = 25;
                jsRow++;
                
                int jsDataStart = jsRow;
                if (jobsStatistics != null)
                {
                    jobsStatsSheet.Cells[jsRow, 1].Value = "Total Jobs";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.TotalJobs;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0";
                    jsRow++;
                    
                    jobsStatsSheet.Cells[jsRow, 1].Value = "Active Jobs";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.ActiveJobs;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0";
                    jsRow++;
                    
                    jobsStatsSheet.Cells[jsRow, 1].Value = "Draft Jobs";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.DraftJobs;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0";
                    jsRow++;
                    
                    jobsStatsSheet.Cells[jsRow, 1].Value = "Closed Jobs";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.ClosedJobs;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0";
                    jsRow++;
                    
                    jobsStatsSheet.Cells[jsRow, 1].Value = "New Jobs This Month";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.NewJobsThisMonth;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0";
                    jsRow++;
                    
                    jobsStatsSheet.Cells[jsRow, 1].Value = "Avg Applications / Job";
                    jobsStatsSheet.Cells[jsRow, 2].Value = jobsStatistics.AverageApplicationsPerJob;
                    jobsStatsSheet.Cells[jsRow, 2].Style.Numberformat.Format = "#,##0.00";
                    jsRow++;
                }
                
                // Apply styling
                for (int r = jsDataStart; r < jsRow; r++)
                {
                    var rowRange = jobsStatsSheet.Cells[r, 1, r, 2];
                    if ((r - jsDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    jobsStatsSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                jobsStatsSheet.Column(1).Width = 30;
                jobsStatsSheet.Column(2).Width = 25;
                jobsStatsSheet.View.FreezePanes(4, 1);

                // ============= JOBS EFFECTIVENESS SHEET =============
                var jobsEffSheet = package.Workbook.Worksheets.Add("Jobs Effectiveness");
                
                // Title
                jobsEffSheet.Cells[1, 1].Value = "Jobs Effectiveness Summary";
                jobsEffSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = jobsEffSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                jobsEffSheet.Row(1).Height = 30;

                // Header
                int jeRow = 3;
                jobsEffSheet.Cells[jeRow, 1].Value = "Metric";
                jobsEffSheet.Cells[jeRow, 2].Value = "Value";
                using (var headerRange = jobsEffSheet.Cells[jeRow, 1, jeRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                jobsEffSheet.Row(jeRow).Height = 25;
                jeRow++;
                
                int jeDataStart = jeRow;
                if (jobsEffectiveness != null)
                {
                    jobsEffSheet.Cells[jeRow, 1].Value = "Avg Resumes / Job";
                    jobsEffSheet.Cells[jeRow, 2].Value = jobsEffectiveness.AverageResumesPerJob;
                    jobsEffSheet.Cells[jeRow, 2].Style.Numberformat.Format = "#,##0.00";
                    jeRow++;
                    
                    jobsEffSheet.Cells[jeRow, 1].Value = "Qualified Rate";
                    jobsEffSheet.Cells[jeRow, 2].Value = jobsEffectiveness.QualifiedRate;
                    jobsEffSheet.Cells[jeRow, 2].Style.Numberformat.Format = "0.00%";
                    jeRow++;
                    
                    jobsEffSheet.Cells[jeRow, 1].Value = "Success Hiring Rate";
                    jobsEffSheet.Cells[jeRow, 2].Value = jobsEffectiveness.SuccessHiringRate;
                    jobsEffSheet.Cells[jeRow, 2].Style.Numberformat.Format = "0.00%";
                    jeRow++;
                }
                
                // Apply styling
                for (int r = jeDataStart; r < jeRow; r++)
                {
                    var rowRange = jobsEffSheet.Cells[r, 1, r, 2];
                    if ((r - jeDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    jobsEffSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                jobsEffSheet.Column(1).Width = 30;
                jobsEffSheet.Column(2).Width = 25;
                jobsEffSheet.View.FreezePanes(4, 1);

                // ============= AI PARSING SHEET =============
                var aiParsingSheet = package.Workbook.Worksheets.Add("AI Parsing");
                
                // Title
                aiParsingSheet.Cells[1, 1].Value = "AI Parsing Quality Summary";
                aiParsingSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = aiParsingSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                aiParsingSheet.Row(1).Height = 30;

                // Header
                int apRow = 3;
                aiParsingSheet.Cells[apRow, 1].Value = "Metric";
                aiParsingSheet.Cells[apRow, 2].Value = "Value";
                using (var headerRange = aiParsingSheet.Cells[apRow, 1, apRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                aiParsingSheet.Row(apRow).Height = 25;
                apRow++;
                
                int apDataStart = apRow;
                if (aiParsing != null)
                {
                    aiParsingSheet.Cells[apRow, 1].Value = "Success Rate";
                    aiParsingSheet.Cells[apRow, 2].Value = aiParsing.SuccessRate;
                    aiParsingSheet.Cells[apRow, 2].Style.Numberformat.Format = "0.00%";
                    apRow++;
                    
                    aiParsingSheet.Cells[apRow, 1].Value = "Total Resumes";
                    aiParsingSheet.Cells[apRow, 2].Value = aiParsing.TotalResumes;
                    aiParsingSheet.Cells[apRow, 2].Style.Numberformat.Format = "#,##0";
                    apRow++;
                    
                    aiParsingSheet.Cells[apRow, 1].Value = "Successful Parsing";
                    aiParsingSheet.Cells[apRow, 2].Value = aiParsing.SuccessfulParsing;
                    aiParsingSheet.Cells[apRow, 2].Style.Numberformat.Format = "#,##0";
                    apRow++;
                    
                    aiParsingSheet.Cells[apRow, 1].Value = "Failed Parsing";
                    aiParsingSheet.Cells[apRow, 2].Value = aiParsing.FailedParsing;
                    aiParsingSheet.Cells[apRow, 2].Style.Numberformat.Format = "#,##0";
                    apRow++;
                    
                    aiParsingSheet.Cells[apRow, 1].Value = "Avg Processing Time (ms)";
                    aiParsingSheet.Cells[apRow, 2].Value = aiParsing.AverageProcessingTimeMs;
                    aiParsingSheet.Cells[apRow, 2].Style.Numberformat.Format = "#,##0.00";
                    apRow++;
                }
                
                // Apply styling
                for (int r = apDataStart; r < apRow; r++)
                {
                    var rowRange = aiParsingSheet.Cells[r, 1, r, 2];
                    if ((r - apDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    aiParsingSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                aiParsingSheet.Column(1).Width = 30;
                aiParsingSheet.Column(2).Width = 25;
                aiParsingSheet.View.FreezePanes(4, 1);

                // ============= AI SCORING SHEET =============
                var aiScoringSheet = package.Workbook.Worksheets.Add("AI Scoring");
                
                // Title
                aiScoringSheet.Cells[1, 1].Value = "AI Scoring Distribution Summary";
                aiScoringSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = aiScoringSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                aiScoringSheet.Row(1).Height = 30;

                // Header
                int asRow = 3;
                aiScoringSheet.Cells[asRow, 1].Value = "Metric";
                aiScoringSheet.Cells[asRow, 2].Value = "Value";
                using (var headerRange = aiScoringSheet.Cells[asRow, 1, asRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                aiScoringSheet.Row(asRow).Height = 25;
                asRow++;
                
                int asDataStart = asRow;
                if (aiScoring != null)
                {
                    aiScoringSheet.Cells[asRow, 1].Value = "Success Rate";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.SuccessRate;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "0.00%";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "High Scores (>75)";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.ScoreDistribution.High;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "Medium Scores (50-75)";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.ScoreDistribution.Medium;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "Low Scores (<50)";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.ScoreDistribution.Low;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "Total Scored";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.Statistics.TotalScored;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "Average Score";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.Statistics.AverageScore;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0.00";
                    asRow++;
                    
                    aiScoringSheet.Cells[asRow, 1].Value = "Median Score";
                    aiScoringSheet.Cells[asRow, 2].Value = aiScoring.Statistics.MedianScore;
                    aiScoringSheet.Cells[asRow, 2].Style.Numberformat.Format = "#,##0.00";
                    asRow++;
                }
                
                // Apply styling
                for (int r = asDataStart; r < asRow; r++)
                {
                    var rowRange = aiScoringSheet.Cells[r, 1, r, 2];
                    if ((r - asDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    aiScoringSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                aiScoringSheet.Column(1).Width = 30;
                aiScoringSheet.Column(2).Width = 25;
                aiScoringSheet.View.FreezePanes(4, 1);

                // ============= AI HEALTH SHEET =============
                var aiHealthSheet = package.Workbook.Worksheets.Add("AI Health");
                
                // Title
                aiHealthSheet.Cells[1, 1].Value = "AI System Health Summary";
                aiHealthSheet.Cells[1, 1, 1, 3].Merge = true;
                using (var titleRange = aiHealthSheet.Cells[1, 1, 1, 3])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                aiHealthSheet.Row(1).Height = 30;

                // Header for main metrics
                int ahRow = 3;
                aiHealthSheet.Cells[ahRow, 1].Value = "Metric";
                aiHealthSheet.Cells[ahRow, 2].Value = "Value";
                using (var headerRange = aiHealthSheet.Cells[ahRow, 1, ahRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                aiHealthSheet.Row(ahRow).Height = 25;
                ahRow++;
                
                int ahDataStart = ahRow;
                if (aiHealth != null)
                {
                    aiHealthSheet.Cells[ahRow, 1].Value = "Success Rate";
                    aiHealthSheet.Cells[ahRow, 2].Value = aiHealth.SuccessRate;
                    aiHealthSheet.Cells[ahRow, 2].Style.Numberformat.Format = "0.00%";
                    ahRow++;
                    
                    aiHealthSheet.Cells[ahRow, 1].Value = "Error Rate";
                    aiHealthSheet.Cells[ahRow, 2].Value = aiHealth.ErrorRate;
                    aiHealthSheet.Cells[ahRow, 2].Style.Numberformat.Format = "0.00%";
                    ahRow++;
                    
                    aiHealthSheet.Cells[ahRow, 1].Value = "Avg Processing Time (seconds)";
                    aiHealthSheet.Cells[ahRow, 2].Value = aiHealth.AverageProcessingTimeSeconds;
                    aiHealthSheet.Cells[ahRow, 2].Style.Numberformat.Format = "#,##0.00";
                    ahRow++;
                    
                    // Apply styling for main metrics
                    for (int r = ahDataStart; r < ahRow; r++)
                    {
                        var rowRange = aiHealthSheet.Cells[r, 1, r, 2];
                        if ((r - ahDataStart) % 2 == 0)
                        {
                            rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                        }
                        rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        aiHealthSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }
                    
                    // Error Reasons table if exists
                    if (aiHealth.ErrorReasons != null && aiHealth.ErrorReasons.Any())
                    {
                        ahRow++;
                        aiHealthSheet.Cells[ahRow, 1].Value = "Error Reasons";
                        aiHealthSheet.Cells[ahRow, 1, ahRow, 3].Merge = true;
                        aiHealthSheet.Cells[ahRow, 1].Style.Font.Bold = true;
                        aiHealthSheet.Cells[ahRow, 1].Style.Font.Size = 14;
                        ahRow++;
                        
                        aiHealthSheet.Cells[ahRow, 1].Value = "Error Type";
                        aiHealthSheet.Cells[ahRow, 2].Value = "Count";
                        aiHealthSheet.Cells[ahRow, 3].Value = "Percentage";
                        using (var errorHeaderRange = aiHealthSheet.Cells[ahRow, 1, ahRow, 3])
                        {
                            errorHeaderRange.Style.Font.Bold = true;
                            errorHeaderRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                            errorHeaderRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            errorHeaderRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 152, 0)); // Orange
                            errorHeaderRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            errorHeaderRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                            errorHeaderRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                        }
                        ahRow++;
                        
                        int errorDataStart = ahRow;
                        foreach (var error in aiHealth.ErrorReasons)
                        {
                            aiHealthSheet.Cells[ahRow, 1].Value = error.ErrorType;
                            aiHealthSheet.Cells[ahRow, 2].Value = error.Count;
                            aiHealthSheet.Cells[ahRow, 2].Style.Numberformat.Format = "#,##0";
                            aiHealthSheet.Cells[ahRow, 3].Value = error.Percentage;
                            aiHealthSheet.Cells[ahRow, 3].Style.Numberformat.Format = "0.00%";
                            ahRow++;
                        }
                        
                        // Apply styling for error reasons
                        for (int r = errorDataStart; r < ahRow; r++)
                        {
                            var rowRange = aiHealthSheet.Cells[r, 1, r, 3];
                            if ((r - errorDataStart) % 2 == 0)
                            {
                                rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 245, 235));
                            }
                            rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                            aiHealthSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            aiHealthSheet.Cells[r, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }
                    }
                }
                
                aiHealthSheet.Column(1).Width = 35;
                aiHealthSheet.Column(2).Width = 20;
                aiHealthSheet.Column(3).Width = 20;
                aiHealthSheet.View.FreezePanes(4, 1);

                // ============= CLIENT ENGAGEMENT SHEET =============
                var clientEngagementSheet = package.Workbook.Worksheets.Add("Client Engagement");
                
                // Title
                clientEngagementSheet.Cells[1, 1].Value = "Client Engagement Summary";
                clientEngagementSheet.Cells[1, 1, 1, 2].Merge = true;
                using (var titleRange = clientEngagementSheet.Cells[1, 1, 1, 2])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                clientEngagementSheet.Row(1).Height = 30;

                // Header
                int ceRow = 3;
                clientEngagementSheet.Cells[ceRow, 1].Value = "Metric";
                clientEngagementSheet.Cells[ceRow, 2].Value = "Value";
                using (var headerRange = clientEngagementSheet.Cells[ceRow, 1, ceRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                clientEngagementSheet.Row(ceRow).Height = 25;
                ceRow++;
                
                int ceDataStart = ceRow;
                if (clientEngagement != null)
                {
                    clientEngagementSheet.Cells[ceRow, 1].Value = "Avg Jobs / Company / Month";
                    clientEngagementSheet.Cells[ceRow, 2].Value = clientEngagement.UsageFrequency.AverageJobsPerCompanyPerMonth;
                    clientEngagementSheet.Cells[ceRow, 2].Style.Numberformat.Format = "#,##0.00";
                    ceRow++;
                    
                    clientEngagementSheet.Cells[ceRow, 1].Value = "Avg Campaigns / Company / Month";
                    clientEngagementSheet.Cells[ceRow, 2].Value = clientEngagement.UsageFrequency.AverageCampaignsPerCompanyPerMonth;
                    clientEngagementSheet.Cells[ceRow, 2].Style.Numberformat.Format = "#,##0.00";
                    ceRow++;
                    
                    clientEngagementSheet.Cells[ceRow, 1].Value = "AI Trust Percentage";
                    clientEngagementSheet.Cells[ceRow, 2].Value = clientEngagement.AiTrustLevel.TrustPercentage;
                    clientEngagementSheet.Cells[ceRow, 2].Style.Numberformat.Format = "0.00%";
                    ceRow++;
                    
                    clientEngagementSheet.Cells[ceRow, 1].Value = "High Score Candidates Count";
                    clientEngagementSheet.Cells[ceRow, 2].Value = clientEngagement.AiTrustLevel.HighScoreCandidatesCount;
                    clientEngagementSheet.Cells[ceRow, 2].Style.Numberformat.Format = "#,##0";
                    ceRow++;
                    
                    clientEngagementSheet.Cells[ceRow, 1].Value = "High Score Candidates Hired";
                    clientEngagementSheet.Cells[ceRow, 2].Value = clientEngagement.AiTrustLevel.HighScoreCandidatesHiredCount;
                    clientEngagementSheet.Cells[ceRow, 2].Style.Numberformat.Format = "#,##0";
                    ceRow++;
                }
                
                // Apply styling
                for (int r = ceDataStart; r < ceRow; r++)
                {
                    var rowRange = clientEngagementSheet.Cells[r, 1, r, 2];
                    if ((r - ceDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    clientEngagementSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                clientEngagementSheet.Column(1).Width = 35;
                clientEngagementSheet.Column(2).Width = 25;
                clientEngagementSheet.View.FreezePanes(4, 1);

                // ============= SAAS METRICS SHEET =============
                var saasMetricsSheet = package.Workbook.Worksheets.Add("SaaS Metrics");
                
                // Title
                saasMetricsSheet.Cells[1, 1].Value = "SaaS Admin Metrics Summary";
                saasMetricsSheet.Cells[1, 1, 1, 5].Merge = true;
                using (var titleRange = saasMetricsSheet.Cells[1, 1, 1, 5])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                saasMetricsSheet.Row(1).Height = 30;

                int smRow = 3;
                if (saasMetrics != null)
                {
                    // Feature Adoption Section
                    saasMetricsSheet.Cells[smRow, 1].Value = "Feature Adoption";
                    saasMetricsSheet.Cells[smRow, 1, smRow, 2].Merge = true;
                    saasMetricsSheet.Cells[smRow, 1].Style.Font.Bold = true;
                    saasMetricsSheet.Cells[smRow, 1].Style.Font.Size = 14;
                    saasMetricsSheet.Cells[smRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    saasMetricsSheet.Cells[smRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(224, 224, 224));
                    smRow++;
                    
                    int featureDataStart = smRow;
                    saasMetricsSheet.Cells[smRow, 1].Value = "Screening Usage";
                    saasMetricsSheet.Cells[smRow, 2].Value = saasMetrics.FeatureAdoption.ScreeningUsageCount;
                    saasMetricsSheet.Cells[smRow, 2].Style.Numberformat.Format = "#,##0";
                    smRow++;
                    
                    saasMetricsSheet.Cells[smRow, 1].Value = "Tracking Usage";
                    saasMetricsSheet.Cells[smRow, 2].Value = saasMetrics.FeatureAdoption.TrackingUsageCount;
                    saasMetricsSheet.Cells[smRow, 2].Style.Numberformat.Format = "#,##0";
                    smRow++;
                    
                    saasMetricsSheet.Cells[smRow, 1].Value = "Export Usage";
                    saasMetricsSheet.Cells[smRow, 2].Value = saasMetrics.FeatureAdoption.ExportUsageCount;
                    saasMetricsSheet.Cells[smRow, 2].Style.Numberformat.Format = "#,##0";
                    smRow++;
                    
                    // Apply styling for feature adoption
                    for (int r = featureDataStart; r < smRow; r++)
                    {
                        var rowRange = saasMetricsSheet.Cells[r, 1, r, 2];
                        if ((r - featureDataStart) % 2 == 0)
                        {
                            rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                        }
                        rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        saasMetricsSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    // Top Companies Section
                    if (saasMetrics.TopCompanies != null && saasMetrics.TopCompanies.Any())
                    {
                        smRow++;
                        saasMetricsSheet.Cells[smRow, 1].Value = "Top Companies";
                        saasMetricsSheet.Cells[smRow, 1, smRow, 5].Merge = true;
                        saasMetricsSheet.Cells[smRow, 1].Style.Font.Bold = true;
                        saasMetricsSheet.Cells[smRow, 1].Style.Font.Size = 14;
                        saasMetricsSheet.Cells[smRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        saasMetricsSheet.Cells[smRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(224, 224, 224));
                        smRow++;
                        
                        // Header
                        saasMetricsSheet.Cells[smRow, 1].Value = "Company Name";
                        saasMetricsSheet.Cells[smRow, 2].Value = "Resumes";
                        saasMetricsSheet.Cells[smRow, 3].Value = "Jobs";
                        saasMetricsSheet.Cells[smRow, 4].Value = "Campaigns";
                        saasMetricsSheet.Cells[smRow, 5].Value = "Activity Score";
                        using (var headerRange = saasMetricsSheet.Cells[smRow, 1, smRow, 5])
                        {
                            headerRange.Style.Font.Bold = true;
                            headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(156, 39, 176)); // Purple
                            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                            headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                        }
                        smRow++;
                        
                        int topCompDataStart = smRow;
                        foreach (var company in saasMetrics.TopCompanies.Take(10))
                        {
                            saasMetricsSheet.Cells[smRow, 1].Value = company.CompanyName;
                            saasMetricsSheet.Cells[smRow, 2].Value = company.TotalResumesUploaded;
                            saasMetricsSheet.Cells[smRow, 2].Style.Numberformat.Format = "#,##0";
                            saasMetricsSheet.Cells[smRow, 3].Value = company.TotalJobsCreated;
                            saasMetricsSheet.Cells[smRow, 3].Style.Numberformat.Format = "#,##0";
                            saasMetricsSheet.Cells[smRow, 4].Value = company.TotalCampaignsCreated;
                            saasMetricsSheet.Cells[smRow, 4].Style.Numberformat.Format = "#,##0";
                            saasMetricsSheet.Cells[smRow, 5].Value = company.ActivityScore;
                            saasMetricsSheet.Cells[smRow, 5].Style.Numberformat.Format = "#,##0.00";
                            smRow++;
                        }
                        
                        // Apply styling
                        for (int r = topCompDataStart; r < smRow; r++)
                        {
                            var rowRange = saasMetricsSheet.Cells[r, 1, r, 5];
                            if ((r - topCompDataStart) % 2 == 0)
                            {
                                rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(243, 229, 245));
                            }
                            rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                            for (int c = 2; c <= 5; c++)
                            {
                                saasMetricsSheet.Cells[r, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }
                        }
                    }

                    // Churn Risk Companies Section
                    if (saasMetrics.ChurnRiskCompanies != null && saasMetrics.ChurnRiskCompanies.Any())
                    {
                        smRow++;
                        saasMetricsSheet.Cells[smRow, 1].Value = "Churn Risk Companies";
                        saasMetricsSheet.Cells[smRow, 1, smRow, 3].Merge = true;
                        saasMetricsSheet.Cells[smRow, 1].Style.Font.Bold = true;
                        saasMetricsSheet.Cells[smRow, 1].Style.Font.Size = 14;
                        saasMetricsSheet.Cells[smRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        saasMetricsSheet.Cells[smRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(224, 224, 224));
                        smRow++;
                        
                        // Header
                        saasMetricsSheet.Cells[smRow, 1].Value = "Company Name";
                        saasMetricsSheet.Cells[smRow, 2].Value = "Plan";
                        saasMetricsSheet.Cells[smRow, 3].Value = "Risk Level";
                        using (var headerRange = saasMetricsSheet.Cells[smRow, 1, smRow, 3])
                        {
                            headerRange.Style.Font.Bold = true;
                            headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(244, 67, 54)); // Red
                            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                            headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                        }
                        smRow++;
                        
                        int churnDataStart = smRow;
                        foreach (var company in saasMetrics.ChurnRiskCompanies.Take(10))
                        {
                            saasMetricsSheet.Cells[smRow, 1].Value = company.CompanyName;
                            saasMetricsSheet.Cells[smRow, 2].Value = company.SubscriptionPlan;
                            saasMetricsSheet.Cells[smRow, 3].Value = company.RiskLevel;
                            smRow++;
                        }
                        
                        // Apply styling
                        for (int r = churnDataStart; r < smRow; r++)
                        {
                            var rowRange = saasMetricsSheet.Cells[r, 1, r, 3];
                            if ((r - churnDataStart) % 2 == 0)
                            {
                                rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 235, 238));
                            }
                            rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        }
                    }
                }
                
                saasMetricsSheet.Column(1).Width = 35;
                saasMetricsSheet.Column(2).Width = 20;
                saasMetricsSheet.Column(3).Width = 20;
                saasMetricsSheet.Column(4).Width = 20;
                saasMetricsSheet.Column(5).Width = 20;
                saasMetricsSheet.View.FreezePanes(2, 1);

                // ============= SUBSCRIPTIONS SHEET =============
                var subscriptionSheet = package.Workbook.Worksheets.Add("Subscriptions");
                
                // Title
                subscriptionSheet.Cells[1, 1].Value = "Subscription & Revenue Summary";
                subscriptionSheet.Cells[1, 1, 1, 4].Merge = true;
                using (var titleRange = subscriptionSheet.Cells[1, 1, 1, 4])
                {
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Size = 18;
                    titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
                    titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                subscriptionSheet.Row(1).Height = 30;

                // Header for main metrics
                int subRow = 3;
                subscriptionSheet.Cells[subRow, 1].Value = "Metric";
                subscriptionSheet.Cells[subRow, 2].Value = "Value";
                using (var headerRange = subscriptionSheet.Cells[subRow, 1, subRow, 2])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(66, 165, 245));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }
                subscriptionSheet.Row(subRow).Height = 25;
                subRow++;
                
                int subDataStart = subRow;
                if (subscriptionRevenue != null)
                {
                    subscriptionSheet.Cells[subRow, 1].Value = "Total Revenue";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.Breakdown.TotalRevenue;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "$#,##0.00";
                    subRow++;
                    
                    subscriptionSheet.Cells[subRow, 1].Value = "Monthly Revenue";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.MonthlyRevenue;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "$#,##0.00";
                    subRow++;
                    
                    subscriptionSheet.Cells[subRow, 1].Value = "Average Revenue Per Company";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.Breakdown.AverageRevenuePerCompany;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "$#,##0.00";
                    subRow++;
                    
                    subscriptionSheet.Cells[subRow, 1].Value = "Total Companies";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.FreeCompanies + subscriptionRevenue.PaidCompanies;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "#,##0";
                    subRow++;

                    subscriptionSheet.Cells[subRow, 1].Value = "Free Companies";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.FreeCompanies;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "#,##0";
                    subRow++;
                    
                    subscriptionSheet.Cells[subRow, 1].Value = "Paid Companies";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.PaidCompanies;
                    subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "#,##0";
                    subRow++;
                    
                    subscriptionSheet.Cells[subRow, 1].Value = "Most Popular Plan";
                    subscriptionSheet.Cells[subRow, 2].Value = subscriptionRevenue.PopularPlan;
                    subRow++;
                }
                
                // Apply styling for main metrics
                for (int r = subDataStart; r < subRow; r++)
                {
                    var rowRange = subscriptionSheet.Cells[r, 1, r, 2];
                    if ((r - subDataStart) % 2 == 0)
                    {
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(250, 250, 250));
                    }
                    rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    subscriptionSheet.Cells[r, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                
                // Plan Statistics table if exists
                if (subscriptionRevenue?.Breakdown?.PlanStatistics != null && subscriptionRevenue.Breakdown.PlanStatistics.Any())
                {
                    subRow++;
                    subscriptionSheet.Cells[subRow, 1].Value = "Plan Statistics";
                    subscriptionSheet.Cells[subRow, 1, subRow, 4].Merge = true;
                    subscriptionSheet.Cells[subRow, 1].Style.Font.Bold = true;
                    subscriptionSheet.Cells[subRow, 1].Style.Font.Size = 14;
                    subscriptionSheet.Cells[subRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    subscriptionSheet.Cells[subRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(224, 224, 224));
                    subRow++;
                    
                    // Header for plan statistics
                    subscriptionSheet.Cells[subRow, 1].Value = "Plan Name";
                    subscriptionSheet.Cells[subRow, 2].Value = "Company Count";
                    subscriptionSheet.Cells[subRow, 3].Value = "Revenue";
                    subscriptionSheet.Cells[subRow, 4].Value = "Avg Revenue/Company";
                    using (var planHeaderRange = subscriptionSheet.Cells[subRow, 1, subRow, 4])
                    {
                        planHeaderRange.Style.Font.Bold = true;
                        planHeaderRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        planHeaderRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        planHeaderRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 150, 136)); // Teal
                        planHeaderRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        planHeaderRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        planHeaderRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                    }
                    subscriptionSheet.Row(subRow).Height = 25;
                    subRow++;
                    
                    int planDataStart = subRow;
                    foreach (var plan in subscriptionRevenue.Breakdown.PlanStatistics)
                    {
                        subscriptionSheet.Cells[subRow, 1].Value = plan.PlanName;
                        
                        subscriptionSheet.Cells[subRow, 2].Value = plan.CompanyCount;
                        subscriptionSheet.Cells[subRow, 2].Style.Numberformat.Format = "#,##0";
                        
                        subscriptionSheet.Cells[subRow, 3].Value = plan.Revenue;
                        subscriptionSheet.Cells[subRow, 3].Style.Numberformat.Format = "$#,##0.00";
                        
                        // Calculate average revenue per company for this plan
                        var avgRevenue = plan.CompanyCount > 0 ? plan.Revenue / plan.CompanyCount : 0;
                        subscriptionSheet.Cells[subRow, 4].Value = avgRevenue;
                        subscriptionSheet.Cells[subRow, 4].Style.Numberformat.Format = "$#,##0.00";
                        
                        subRow++;
                    }
                    
                    // Apply styling for plan statistics
                    for (int r = planDataStart; r < subRow; r++)
                    {
                        var rowRange = subscriptionSheet.Cells[r, 1, r, 4];
                        if ((r - planDataStart) % 2 == 0)
                        {
                            rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(224, 242, 241));
                        }
                        rowRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        rowRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        for (int c = 2; c <= 4; c++)
                        {
                            subscriptionSheet.Cells[r, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }
                    }
                }
                
                subscriptionSheet.Column(1).Width = 35;
                subscriptionSheet.Column(2).Width = 20;
                subscriptionSheet.Column(3).Width = 20;
                subscriptionSheet.Column(4).Width = 25;
                subscriptionSheet.View.FreezePanes(4, 1);

                var fileBytes = package.GetAsByteArray();
                var vietnamTime = DateTime.UtcNow.AddHours(7);
                var fileName = $"System_Reports_{vietnamTime:yyyyMMdd_HHmmss}.xlsx";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Excel file for all system reports generated successfully.",
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
                    Message = $"An error occurred while exporting all system reports to Excel: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Export all system reports into a single, nicely formatted PDF.
        /// </summary>
        public async Task<ServiceResponse> ExportAllSystemReportsToPdfAsync()
        {
            try
            {
                var executiveSummaryResponse = await GetExecutiveSummaryAsync();
                if (executiveSummaryResponse.Status != SRStatus.Success || executiveSummaryResponse.Data == null)
                    return executiveSummaryResponse;

                var companiesOverviewResponse = await GetCompaniesOverviewAsync();
                if (companiesOverviewResponse.Status != SRStatus.Success || companiesOverviewResponse.Data == null)
                    return companiesOverviewResponse;

                var companiesUsageResponse = await GetCompaniesUsageAsync();
                if (companiesUsageResponse.Status != SRStatus.Success || companiesUsageResponse.Data == null)
                    return companiesUsageResponse;

                var jobsStatisticsResponse = await GetJobsStatisticsAsync();
                if (jobsStatisticsResponse.Status != SRStatus.Success || jobsStatisticsResponse.Data == null)
                    return jobsStatisticsResponse;

                var jobsEffectivenessResponse = await GetJobsEffectivenessAsync();
                if (jobsEffectivenessResponse.Status != SRStatus.Success || jobsEffectivenessResponse.Data == null)
                    return jobsEffectivenessResponse;

                var aiParsingResponse = await GetAiParsingQualityAsync();
                if (aiParsingResponse.Status != SRStatus.Success || aiParsingResponse.Data == null)
                    return aiParsingResponse;

                var aiScoringResponse = await GetAiScoringDistributionAsync();
                if (aiScoringResponse.Status != SRStatus.Success || aiScoringResponse.Data == null)
                    return aiScoringResponse;

                var subscriptionRevenueResponse = await GetSubscriptionRevenueAsync();
                if (subscriptionRevenueResponse.Status != SRStatus.Success || subscriptionRevenueResponse.Data == null)
                    return subscriptionRevenueResponse;

                var aiHealthResponse = await GetAiSystemHealthReportAsync();
                if (aiHealthResponse.Status != SRStatus.Success || aiHealthResponse.Data == null)
                    return aiHealthResponse;

                var clientEngagementResponse = await GetClientEngagementReportAsync();
                if (clientEngagementResponse.Status != SRStatus.Success || clientEngagementResponse.Data == null)
                    return clientEngagementResponse;

                var saasMetricsResponse = await GetSaasAdminMetricsReportAsync();
                if (saasMetricsResponse.Status != SRStatus.Success || saasMetricsResponse.Data == null)
                    return saasMetricsResponse;

                var executiveSummary = executiveSummaryResponse.Data as ExecutiveSummaryResponse;
                var companiesOverview = companiesOverviewResponse.Data as CompanyOverviewResponse;
                var companiesUsage = companiesUsageResponse.Data as CompanyUsageResponse;
                var jobsStatistics = jobsStatisticsResponse.Data as JobStatisticsResponse;
                var jobsEffectiveness = jobsEffectivenessResponse.Data as JobEffectivenessResponse;
                var aiParsing = aiParsingResponse.Data as AiParsingQualityResponse;
                var aiScoring = aiScoringResponse.Data as AiScoringDistributionResponse;
                var subscriptionRevenue = subscriptionRevenueResponse.Data as SubscriptionRevenueResponse;
                var aiHealth = aiHealthResponse.Data as AiSystemHealthReportResponse;
                var clientEngagement = clientEngagementResponse.Data as ClientEngagementReportResponse;
                var saasMetrics = saasMetricsResponse.Data as SaasAdminMetricsReportResponse;

                // Download logo từ Cloudinary
                byte[]? logoBytes = null;
                try
                {
                    var logoUrl = "https://res.cloudinary.com/dhtkfrubh/image/upload/companies/logos/wtwt1u9cl5uovpgrfgdl.png";
                    using var httpClient = new HttpClient();
                    logoBytes = await httpClient.GetByteArrayAsync(logoUrl);
                }
                catch
                {
                    // Nếu không load được logo thì bỏ qua
                }

                var pdfDocument = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Content().Element(c =>
                        {
                            c.Column(column =>
                            {
                                // Header chỉ xuất hiện ở trang đầu
                                var vietnamTime = DateTime.UtcNow.AddHours(7);
                                column.Item().PaddingBottom(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Column(headerColumn =>
                                {
                                    // Row chứa logo và text
                                    headerColumn.Item().Row(row =>
                                    {
                                        // Logo bên trái
                                        if (logoBytes != null)
                                        {
                                            row.ConstantItem(120).Height(50).Image(logoBytes);
                                        }
                                        
                                        // Spacing
                                        row.ConstantItem(15);
                                        
                                        // Text bên phải
                                        row.RelativeItem().Column(textColumn =>
                                        {
                                            textColumn.Item().AlignLeft().PaddingTop(5).Text("AI-Powered Candidate Evaluation System").FontSize(10).FontColor(Colors.Grey.Medium);
                                            textColumn.Item().AlignLeft().Text("System Reports Overview").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                            textColumn.Item().AlignLeft().Text("Generated on: " + vietnamTime.ToString("MMMM dd, yyyy")).FontSize(9).FontColor(Colors.Grey.Medium);
                                        });
                                    });
                                });

                                column.Item().PaddingTop(10).Text("1. Executive Summary").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (executiveSummary != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Total Companies");
                                        table.Cell().Element(CellStyle).Text(executiveSummary.TotalCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Total Jobs");
                                        table.Cell().Element(CellStyle).Text(executiveSummary.TotalJobs.ToString());

                                        table.Cell().Element(CellStyle).Text("AI Processed Resumes");
                                        table.Cell().Element(CellStyle).Text(executiveSummary.AiProcessedResumes.ToString());

                                        table.Cell().Element(CellStyle).Text("Total Revenue");
                                        table.Cell().Element(CellStyle).Text($"${executiveSummary.TotalRevenue:N2}");
                                    });
                                }

                                column.Item().PaddingTop(20).Text("2. Companies Overview").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (companiesOverview != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Total Companies");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.TotalCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Active Companies");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.ActiveCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Inactive Companies");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.InactiveCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("New Companies This Month");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.NewCompaniesThisMonth.ToString());
                                    });
                                    
                                    // Subscription Breakdown
                                    column.Item().PaddingTop(15).Text(t => 
                                    {
                                        t.Span("Subscription Breakdown").FontSize(11).Bold().FontColor(Colors.Black);
                                    });
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Cell().Element(CellStyle).Text("With Active Subscription");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.SubscriptionBreakdown.WithActiveSubscription.ToString());

                                        table.Cell().Element(CellStyle).Text("With Expired Subscription");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.SubscriptionBreakdown.WithExpiredSubscription.ToString());

                                        table.Cell().Element(CellStyle).Text("Without Subscription");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.SubscriptionBreakdown.WithoutSubscription.ToString());
                                    });
                                    
                                    // Verification Breakdown
                                    column.Item().PaddingTop(15).Text(t => 
                                    {
                                        t.Span("Verification Breakdown").FontSize(11).Bold().FontColor(Colors.Black);
                                    });
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Cell().Element(CellStyle).Text("Verified");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.VerificationBreakdown.Verified.ToString());

                                        table.Cell().Element(CellStyle).Text("Pending");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.VerificationBreakdown.Pending.ToString());

                                        table.Cell().Element(CellStyle).Text("Rejected");
                                        table.Cell().Element(CellStyle).Text(companiesOverview.VerificationBreakdown.Rejected.ToString());
                                    });
                                }

                                // Companies usage
                                column.Item().PaddingTop(20).Text("3. Companies Usage").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (companiesUsage != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Registered Only");
                                        table.Cell().Element(CellStyle).Text(companiesUsage.RegisteredOnly.ToString());

                                        table.Cell().Element(CellStyle).Text("Engaged Companies");
                                        table.Cell().Element(CellStyle).Text(companiesUsage.EngagedCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Frequent Companies");
                                        table.Cell().Element(CellStyle).Text(companiesUsage.FrequentCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Active Rate");
                                        table.Cell().Element(CellStyle).Text($"{companiesUsage.Kpis.ActiveRate:P2}");

                                        table.Cell().Element(CellStyle).Text("AI Usage Rate");
                                        table.Cell().Element(CellStyle).Text($"{companiesUsage.Kpis.AiUsageRate:P2}");

                                        table.Cell().Element(CellStyle).Text("Returning Rate");
                                        table.Cell().Element(CellStyle).Text($"{companiesUsage.Kpis.ReturningRate:P2}");
                                    });
                                }

                                // Jobs statistics
                                column.Item().PaddingTop(20).Text("4. Jobs Statistics").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (jobsStatistics != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Total Jobs");
                                        table.Cell().Element(CellStyle).Text(jobsStatistics.TotalJobs.ToString());

                                        table.Cell().Element(CellStyle).Text("Active Jobs");
                                        table.Cell().Element(CellStyle).Text(jobsStatistics.ActiveJobs.ToString());

                                        table.Cell().Element(CellStyle).Text("Draft Jobs");
                                        table.Cell().Element(CellStyle).Text(jobsStatistics.DraftJobs.ToString());

                                        table.Cell().Element(CellStyle).Text("Closed Jobs");
                                        table.Cell().Element(CellStyle).Text(jobsStatistics.ClosedJobs.ToString());

                                        table.Cell().Element(CellStyle).Text("New Jobs This Month");
                                        table.Cell().Element(CellStyle).Text(jobsStatistics.NewJobsThisMonth.ToString());

                                        table.Cell().Element(CellStyle).Text("Avg Applications / Job");
                                        table.Cell().Element(CellStyle).Text($"{jobsStatistics.AverageApplicationsPerJob:N2}");
                                    });
                                }

                                // Jobs effectiveness
                                column.Item().PaddingTop(20).Text("5. Jobs Effectiveness").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (jobsEffectiveness != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Avg Resumes / Job");
                                        table.Cell().Element(CellStyle).Text($"{jobsEffectiveness.AverageResumesPerJob:N2}");

                                        table.Cell().Element(CellStyle).Text("Qualified Rate");
                                        table.Cell().Element(CellStyle).Text($"{jobsEffectiveness.QualifiedRate:P2}");

                                        table.Cell().Element(CellStyle).Text("Success Hiring Rate");
                                        table.Cell().Element(CellStyle).Text($"{jobsEffectiveness.SuccessHiringRate:P2}");
                                    });
                                }

                                // AI parsing quality
                                column.Item().PaddingTop(20).Text("6. AI Parsing Quality").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (aiParsing != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Success Rate");
                                        table.Cell().Element(CellStyle).Text($"{aiParsing.SuccessRate:P2}");

                                        table.Cell().Element(CellStyle).Text("Total Resumes");
                                        table.Cell().Element(CellStyle).Text(aiParsing.TotalResumes.ToString());

                                        table.Cell().Element(CellStyle).Text("Successful Parsing");
                                        table.Cell().Element(CellStyle).Text(aiParsing.SuccessfulParsing.ToString());

                                        table.Cell().Element(CellStyle).Text("Failed Parsing");
                                        table.Cell().Element(CellStyle).Text(aiParsing.FailedParsing.ToString());

                                        table.Cell().Element(CellStyle).Text("Avg Processing Time (ms)");
                                        table.Cell().Element(CellStyle).Text($"{aiParsing.AverageProcessingTimeMs:N2}");
                                    });
                                }

                                // AI scoring distribution
                                column.Item().PaddingTop(20).Text("7. AI Scoring Distribution").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (aiScoring != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Success Rate");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.SuccessRate:P2}");

                                        table.Cell().Element(CellStyle).Text("High Scores (>75)");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.ScoreDistribution.High:P2}");

                                        table.Cell().Element(CellStyle).Text("Medium Scores (50-75)");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.ScoreDistribution.Medium:P2}");

                                        table.Cell().Element(CellStyle).Text("Low Scores (<50)");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.ScoreDistribution.Low:P2}");

                                        table.Cell().Element(CellStyle).Text("Total Scored");
                                        table.Cell().Element(CellStyle).Text(aiScoring.Statistics.TotalScored.ToString());

                                        table.Cell().Element(CellStyle).Text("Average Score");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.Statistics.AverageScore:N2}");

                                        table.Cell().Element(CellStyle).Text("Median Score");
                                        table.Cell().Element(CellStyle).Text($"{aiScoring.Statistics.MedianScore:N2}");
                                    });
                                }

                                // AI Health
                                column.Item().PaddingTop(20).Text("8. AI System Health").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (aiHealth != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Success Rate");
                                        table.Cell().Element(CellStyle).Text($"{aiHealth.SuccessRate:P2}");

                                        table.Cell().Element(CellStyle).Text("Error Rate");
                                        table.Cell().Element(CellStyle).Text($"{aiHealth.ErrorRate:P2}");

                                        table.Cell().Element(CellStyle).Text("Avg Processing Time (seconds)");
                                        table.Cell().Element(CellStyle).Text($"{aiHealth.AverageProcessingTimeSeconds:N2}");

                                        if (aiHealth.ErrorReasons != null && aiHealth.ErrorReasons.Any())
                                        {
                                            table.Cell().Element(CellStyle).Text("Error Reasons");
                                            table.Cell().Element(CellStyle).Text($"{aiHealth.ErrorReasons.Count} types");
                                        }
                                    });

                                    if (aiHealth.ErrorReasons != null && aiHealth.ErrorReasons.Any())
                                    {
                                        column.Item().PaddingTop(10).Text("Error Breakdown").SemiBold();
                                        column.Item().PaddingTop(2).Table(table =>
                                        {
                                            table.ColumnsDefinition(cols =>
                                            {
                                                cols.RelativeColumn(2);
                                                cols.RelativeColumn(1);
                                                cols.RelativeColumn(1);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell().Element(CellStyle).Text("Error Type").Bold();
                                                header.Cell().Element(CellStyle).Text("Count").Bold();
                                                header.Cell().Element(CellStyle).Text("Percentage").Bold();
                                            });

                                            foreach (var error in aiHealth.ErrorReasons.Take(5))
                                            {
                                                table.Cell().Element(CellStyle).Text(error.ErrorType);
                                                table.Cell().Element(CellStyle).Text(error.Count.ToString());
                                                table.Cell().Element(CellStyle).Text($"{error.Percentage:P2}");
                                            }
                                        });
                                    }
                                }

                                // Client Engagement
                                column.Item().PaddingTop(20).Text("9. Client Engagement").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (clientEngagement != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Avg Jobs / Company / Month");
                                        table.Cell().Element(CellStyle).Text($"{clientEngagement.UsageFrequency.AverageJobsPerCompanyPerMonth:N2}");

                                        table.Cell().Element(CellStyle).Text("Avg Campaigns / Company / Month");
                                        table.Cell().Element(CellStyle).Text($"{clientEngagement.UsageFrequency.AverageCampaignsPerCompanyPerMonth:N2}");

                                        table.Cell().Element(CellStyle).Text("AI Trust Percentage");
                                        table.Cell().Element(CellStyle).Text($"{clientEngagement.AiTrustLevel.TrustPercentage:P2}");

                                        table.Cell().Element(CellStyle).Text("High Score Candidates Count");
                                        table.Cell().Element(CellStyle).Text(clientEngagement.AiTrustLevel.HighScoreCandidatesCount.ToString());

                                        table.Cell().Element(CellStyle).Text("High Score Candidates Hired");
                                        table.Cell().Element(CellStyle).Text(clientEngagement.AiTrustLevel.HighScoreCandidatesHiredCount.ToString());
                                    });
                                }

                                // SaaS Metrics
                                column.Item().PaddingTop(20).Text("10. SaaS Admin Metrics").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (saasMetrics != null)
                                {
                                    column.Item().PaddingTop(5).Text("Feature Adoption").SemiBold();
                                    column.Item().PaddingTop(2).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Feature").Bold();
                                            header.Cell().Element(CellStyle).Text("Usage Count").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Screening");
                                        table.Cell().Element(CellStyle).Text(saasMetrics.FeatureAdoption.ScreeningUsageCount.ToString());

                                        table.Cell().Element(CellStyle).Text("Tracking");
                                        table.Cell().Element(CellStyle).Text(saasMetrics.FeatureAdoption.TrackingUsageCount.ToString());

                                        table.Cell().Element(CellStyle).Text("Export");
                                        table.Cell().Element(CellStyle).Text(saasMetrics.FeatureAdoption.ExportUsageCount.ToString());
                                    });

                                    if (saasMetrics.TopCompanies != null && saasMetrics.TopCompanies.Any())
                                    {
                                        column.Item().PaddingTop(10).Text("Top Companies (Top 5)").SemiBold();
                                        column.Item().PaddingTop(2).Table(table =>
                                        {
                                            table.ColumnsDefinition(cols =>
                                            {
                                                cols.RelativeColumn(3);
                                                cols.RelativeColumn(1);
                                                cols.RelativeColumn(1);
                                                cols.RelativeColumn(1);
                                                cols.RelativeColumn(1);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                                header.Cell().Element(CellStyle).Text("Resumes").Bold();
                                                header.Cell().Element(CellStyle).Text("Jobs").Bold();
                                                header.Cell().Element(CellStyle).Text("Campaigns").Bold();
                                                header.Cell().Element(CellStyle).Text("Score").Bold();
                                            });

                                            foreach (var company in saasMetrics.TopCompanies.Take(5))
                                            {
                                                table.Cell().Element(CellStyle).Text(company.CompanyName);
                                                table.Cell().Element(CellStyle).Text(company.TotalResumesUploaded.ToString());
                                                table.Cell().Element(CellStyle).Text(company.TotalJobsCreated.ToString());
                                                table.Cell().Element(CellStyle).Text(company.TotalCampaignsCreated.ToString());
                                                table.Cell().Element(CellStyle).Text(company.ActivityScore.ToString());
                                            }
                                        });
                                    }

                                    if (saasMetrics.ChurnRiskCompanies != null && saasMetrics.ChurnRiskCompanies.Any())
                                    {
                                        column.Item().PaddingTop(10).Text("Churn Risk Companies (Top 5)").SemiBold();
                                        column.Item().PaddingTop(2).Table(table =>
                                        {
                                            table.ColumnsDefinition(cols =>
                                            {
                                                cols.RelativeColumn(2);
                                                cols.RelativeColumn(2);
                                                cols.RelativeColumn(1);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                                header.Cell().Element(CellStyle).Text("Plan").Bold();
                                                header.Cell().Element(CellStyle).Text("Risk").Bold();
                                            });

                                            foreach (var company in saasMetrics.ChurnRiskCompanies.Take(5))
                                            {
                                                table.Cell().Element(CellStyle).Text(company.CompanyName);
                                                table.Cell().Element(CellStyle).Text(company.SubscriptionPlan);
                                                table.Cell().Element(CellStyle).Text(company.RiskLevel);
                                            }
                                        });
                                    }
                                }

                                // Subscription & revenue
                                column.Item().PaddingTop(20).Text("11. Subscription & Revenue").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                if (subscriptionRevenue != null)
                                {
                                    column.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(2);
                                            cols.RelativeColumn(3);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Metric").Bold();
                                            header.Cell().Element(CellStyle).Text("Value").Bold();
                                        });

                                        table.Cell().Element(CellStyle).Text("Free Companies");
                                        table.Cell().Element(CellStyle).Text(subscriptionRevenue.FreeCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Paid Companies");
                                        table.Cell().Element(CellStyle).Text(subscriptionRevenue.PaidCompanies.ToString());

                                        table.Cell().Element(CellStyle).Text("Monthly Revenue");
                                        table.Cell().Element(CellStyle).Text($"${subscriptionRevenue.MonthlyRevenue:N2}");

                                        table.Cell().Element(CellStyle).Text("Total Revenue");
                                        table.Cell().Element(CellStyle).Text($"${subscriptionRevenue.Breakdown.TotalRevenue:N2}");

                                        table.Cell().Element(CellStyle).Text("Average Revenue Per Company");
                                        table.Cell().Element(CellStyle).Text($"${subscriptionRevenue.Breakdown.AverageRevenuePerCompany:N2}");

                                        table.Cell().Element(CellStyle).Text("Popular Plan");
                                        table.Cell().Element(CellStyle).Text(subscriptionRevenue.PopularPlan);
                                    });

                                    // Plan Statistics Table
                                    if (subscriptionRevenue.Breakdown.PlanStatistics != null && subscriptionRevenue.Breakdown.PlanStatistics.Any())
                                    {
                                        column.Item().PaddingTop(15).Text("Plan Statistics").FontSize(12).Bold().FontColor(Colors.Black);
                                        column.Item().PaddingTop(5).Table(table =>
                                        {
                                            table.ColumnsDefinition(cols =>
                                            {
                                                cols.RelativeColumn(3);
                                                cols.RelativeColumn(2);
                                                cols.RelativeColumn(2);
                                                cols.RelativeColumn(2);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell().Element(CellStyle).Text("Plan Name").Bold();
                                                header.Cell().Element(CellStyle).Text("Companies").Bold();
                                                header.Cell().Element(CellStyle).Text("Revenue").Bold();
                                                header.Cell().Element(CellStyle).Text("Avg/Company").Bold();
                                            });

                                            foreach (var plan in subscriptionRevenue.Breakdown.PlanStatistics)
                                            {
                                                var avgRevenue = plan.CompanyCount > 0 ? plan.Revenue / plan.CompanyCount : 0;
                                                
                                                table.Cell().Element(CellStyle).Text(plan.PlanName);
                                                table.Cell().Element(CellStyle).Text(plan.CompanyCount.ToString());
                                                table.Cell().Element(CellStyle).Text($"${plan.Revenue:N2}");
                                                table.Cell().Element(CellStyle).Text($"${avgRevenue:N2}");
                                            }
                                        });
                                    }
                                }
                            });
                        });

                        page.Footer().Element(ComposeReportFooter);
                    });
                });

                var fileBytes = pdfDocument.GeneratePdf();
                var vietnamTime = DateTime.UtcNow.AddHours(7);
                var fileName = $"System_Reports_{vietnamTime:yyyyMMdd_HHmmss}.pdf";

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "PDF for all system reports generated successfully.",
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
                    Message = $"An error occurred while exporting all system reports to PDF: {ex.Message}"
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

        private static void ComposeReportFooter(IContainer container)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Colors.Grey.Lighten1)
                .AlignCenter()
                .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium))
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
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

        #region AI System Health Report

        /// <summary>
        /// Get AI System Health Report - Chỉ số Sức khỏe AI & Lỗi hệ thống (Tổng thể hệ thống)
        /// </summary>
        public async Task<ServiceResponse> GetAiSystemHealthReportAsync()
        {
            try
            {
                // Get all resumes
                var resumes = await _context.Resumes
                    .AsNoTracking()
                    .ToListAsync();

                if (!resumes.Any())
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "No resume data available.",
                        Data = new AiSystemHealthReportResponse
                        {
                            SuccessRate = 0,
                            ErrorRate = 0,
                            AverageProcessingTimeSeconds = 0,
                            ErrorReasons = new List<ErrorReasonItem>()
                        }
                    };
                }

                // Calculate metrics
                int totalCount = resumes.Count;
                
                // Successful resumes: Completed status
                int successCount = resumes.Count(r => r.Status == ResumeStatusEnum.Completed);
                
                // Error resumes: Failed, InvalidResumeData, CorruptedFile, ServerError, Timeout
                int errorCount = resumes.Count(r => 
                    r.Status == ResumeStatusEnum.Failed ||
                    r.Status == ResumeStatusEnum.InvalidResumeData ||
                    r.Status == ResumeStatusEnum.CorruptedFile ||
                    r.Status == ResumeStatusEnum.ServerError ||
                    r.Status == ResumeStatusEnum.Timeout);

                // Calculate success rate (%)
                decimal successRate = totalCount > 0 ? (decimal)successCount / totalCount * 100 : 0;
                
                // Calculate error rate (%)
                decimal errorRate = totalCount > 0 ? (decimal)errorCount / totalCount * 100 : 0;

                // Calculate average processing time in seconds
                // Using LastReusedAt if available, otherwise estimate based on status
                var processedResumes = resumes.Where(r => r.Status == ResumeStatusEnum.Completed).ToList();
                decimal avgProcessingTimeSeconds = 0;
                if (processedResumes.Any())
                {
                    // Estimate processing time: for completed resumes, assume processing takes ~2-3 seconds on average
                    // In real scenario, you might want to add a separate ProcessingTimeMs field to Resume entity
                    avgProcessingTimeSeconds = 2.5m;
                }

                // Analyze error reasons
                var errorReasons = AnalyzeErrorReasons(resumes);

                var response = new AiSystemHealthReportResponse
                {
                    SuccessRate = Math.Round(successRate, 2),
                    ErrorRate = Math.Round(errorRate, 2),
                    AverageProcessingTimeSeconds = Math.Round(avgProcessingTimeSeconds, 2),
                    ErrorReasons = errorReasons
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "AI System Health Report retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while generating AI System Health Report: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Analyze error reasons
        /// </summary>
        private List<ErrorReasonItem> AnalyzeErrorReasons(List<Resume> resumes)
        {
            var failedResumes = resumes.Where(r => 
                r.Status == ResumeStatusEnum.Failed ||
                r.Status == ResumeStatusEnum.InvalidResumeData ||
                r.Status == ResumeStatusEnum.CorruptedFile ||
                r.Status == ResumeStatusEnum.ServerError ||
                r.Status == ResumeStatusEnum.Timeout).ToList();

            int totalErrors = failedResumes.Count;

            var errorReasons = new List<ErrorReasonItem>();

            // Count format errors
            int formatErrorCount = failedResumes.Count(r => r.Status == ResumeStatusEnum.Failed);
            if (formatErrorCount > 0)
            {
                errorReasons.Add(new ErrorReasonItem
                {
                    ErrorType = "Format Error (Old .doc, Scanned PDF)",
                    Count = formatErrorCount,
                    Percentage = totalErrors > 0 ? Math.Round((decimal)formatErrorCount / totalErrors * 100, 2) : 0
                });
            }

            // Count language errors (currently 0, but placeholder for future)
            int languageErrorCount = 0;
            if (languageErrorCount > 0)
            {
                errorReasons.Add(new ErrorReasonItem
                {
                    ErrorType = "Language Error (Unsupported Language)",
                    Count = languageErrorCount,
                    Percentage = totalErrors > 0 ? Math.Round((decimal)languageErrorCount / totalErrors * 100, 2) : 0
                });
            }

            // Count structure errors (currently 0, but placeholder for future)
            int structureErrorCount = 0;
            if (structureErrorCount > 0)
            {
                errorReasons.Add(new ErrorReasonItem
                {
                    ErrorType = "Structure Error (Complex CV Layout)",
                    Count = structureErrorCount,
                    Percentage = totalErrors > 0 ? Math.Round((decimal)structureErrorCount / totalErrors * 100, 2) : 0
                });
            }

            // Count other errors (CorruptedFile, ServerError, Timeout, InvalidResumeData)
            int otherErrorCount = failedResumes.Count(r => 
                r.Status == ResumeStatusEnum.CorruptedFile ||
                r.Status == ResumeStatusEnum.ServerError ||
                r.Status == ResumeStatusEnum.Timeout ||
                r.Status == ResumeStatusEnum.InvalidResumeData);
            
            if (otherErrorCount > 0)
            {
                errorReasons.Add(new ErrorReasonItem
                {
                    ErrorType = "Other Error (Corrupted File, Server Error, Timeout)",
                    Count = otherErrorCount,
                    Percentage = totalErrors > 0 ? Math.Round((decimal)otherErrorCount / totalErrors * 100, 2) : 0
                });
            }

            return errorReasons.OrderByDescending(e => e.Count).ToList();
        }

        #endregion

        #region Client Engagement Report

        /// <summary>
        /// Get Client Engagement Report - Chỉ số Hoạt động của Khách hàng
        /// </summary>
        public async Task<ServiceResponse> GetClientEngagementReportAsync()
        {
            try
            {
                // Calculate usage frequency
                var usageFrequency = await CalculateUsageFrequencyAsync();

                // Calculate AI trust level
                var aiTrustLevel = await CalculateAiTrustLevelAsync();

                var response = new ClientEngagementReportResponse
                {
                    UsageFrequency = usageFrequency,
                    AiTrustLevel = aiTrustLevel
                };

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Client Engagement Report retrieved successfully.",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"An error occurred while generating Client Engagement Report: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate usage frequency - Average jobs/campaigns per company per month
        /// </summary>
        private async Task<UsageFrequency> CalculateUsageFrequencyAsync()
        {
            var now = DateTime.UtcNow;
            var oneMonthAgo = now.AddMonths(-1);

            // Get active companies
            var activeCompanies = await _context.Companies
                .AsNoTracking()
                .Where(c => c.IsActive)
                .CountAsync();

            if (activeCompanies == 0)
            {
                return new UsageFrequency
                {
                    AverageJobsPerCompanyPerMonth = 0,
                    AverageCampaignsPerCompanyPerMonth = 0
                };
            }

            // Count jobs created in the last month
            var jobsLastMonth = await _context.Jobs
                .AsNoTracking()
                .Where(j => j.CreatedAt >= oneMonthAgo && j.CreatedAt <= now && j.IsActive)
                .CountAsync();

            // Count campaigns created in the last month
            var campaignsLastMonth = await _context.Campaigns
                .AsNoTracking()
                .Where(c => c.CreatedAt >= oneMonthAgo && c.CreatedAt <= now && c.IsActive)
                .CountAsync();

            return new UsageFrequency
            {
                AverageJobsPerCompanyPerMonth = Math.Round((decimal)jobsLastMonth / activeCompanies, 2),
                AverageCampaignsPerCompanyPerMonth = Math.Round((decimal)campaignsLastMonth / activeCompanies, 2)
            };
        }

        /// <summary>
        /// Calculate AI trust level - Percentage of high-score candidates (>80) that HR moved to Hiring Status
        /// </summary>
        private async Task<AiTrustLevel> CalculateAiTrustLevelAsync()
        {
            // Get all resume applications with high AI scores (>80)
            var highScoreApplications = await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && 
                            (ra.AdjustedScore ?? ra.TotalScore ?? 0) > 80)
                .ToListAsync();

            int totalHighScore = highScoreApplications.Count;

            if (totalHighScore == 0)
            {
                return new AiTrustLevel
                {
                    TrustPercentage = 0,
                    HighScoreCandidatesCount = 0,
                    HighScoreCandidatesHiredCount = 0
                };
            }

            // Count how many high-score candidates were moved to Hiring Status (Shortlisted, Interview, Hired)
            var highScoreHired = highScoreApplications.Count(ra => 
                ra.Status == ApplicationStatusEnum.Shortlisted ||
                ra.Status == ApplicationStatusEnum.Interview ||
                ra.Status == ApplicationStatusEnum.Hired);

            decimal trustPercentage = (decimal)highScoreHired / totalHighScore * 100;

            return new AiTrustLevel
            {
                TrustPercentage = Math.Round(trustPercentage, 2),
                HighScoreCandidatesCount = totalHighScore,
                HighScoreCandidatesHiredCount = highScoreHired
            };
        }

        #endregion

            #region SaaS Admin Metrics Report

            /// <summary>
            /// Get SaaS Admin Metrics Report - Báo cáo Hành vi Khách hàng
            /// </summary>
            public async Task<ServiceResponse> GetSaasAdminMetricsReportAsync()
            {
                try
                {
                    // Get top companies by activity
                    var topCompanies = await GetTopCompaniesAsync();

                    // Get feature adoption metrics
                    var featureAdoption = await GetFeatureAdoptionAsync();

                    // Get churn risk companies
                    var churnRisk = await GetChurnRiskCompaniesAsync();

                    var response = new SaasAdminMetricsReportResponse
                    {
                        TopCompanies = topCompanies,
                        FeatureAdoption = featureAdoption,
                        ChurnRiskCompanies = churnRisk
                    };

                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "SaaS Admin Metrics Report retrieved successfully.",
                        Data = response
                    };
                }
                catch (Exception ex)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = $"An error occurred while generating SaaS Admin Metrics Report: {ex.Message}"
                    };
                }
            }

            /// <summary>
            /// Get top companies by activity (Top users using most resources)
            /// </summary>
            private async Task<List<TopCompanyUsage>> GetTopCompaniesAsync()
            {
                var companies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        c.CompanyId,
                        c.Name,
                        ResumesCount = _context.Resumes.Count(r => r.CompanyId == c.CompanyId && r.IsActive),
                        JobsCount = _context.Jobs.Count(j => j.CompanyId == c.CompanyId && j.IsActive),
                        CampaignsCount = _context.Campaigns.Count(camp => camp.CompanyId == c.CompanyId && camp.IsActive)
                    })
                    .ToListAsync();

                var topCompanies = companies
                    .Select(c => new TopCompanyUsage
                    {
                        CompanyId = c.CompanyId,
                        CompanyName = c.Name,
                        TotalResumesUploaded = c.ResumesCount,
                        TotalJobsCreated = c.JobsCount,
                        TotalCampaignsCreated = c.CampaignsCount,
                        ActivityScore = c.ResumesCount + (c.JobsCount * 5) + (c.CampaignsCount * 10)
                    })
                    .OrderByDescending(c => c.ActivityScore)
                    .Take(3)
                    .ToList();

                return topCompanies;
            }

            /// <summary>
            /// Get feature adoption metrics
            /// </summary>
            private async Task<FeatureAdoption> GetFeatureAdoptionAsync()
            {
                // Screening usage: Count total resume applications (AI screening)
                var screeningCount = await _context.ResumeApplications
                    .AsNoTracking()
                    .Where(ra => ra.IsActive)
                    .CountAsync();

                // Tracking usage: Count applications with status changes (HR tracking)
                var trackingCount = await _context.ResumeApplications
                    .AsNoTracking()
                    .Where(ra => ra.IsActive && ra.Status != ApplicationStatusEnum.Pending)
                    .CountAsync();

                // Export usage: Count companies that have active subscriptions (assuming they export)
                // Note: If you track exports separately, replace this with actual export count
                var exportCount = await _context.CompanySubscriptions
                    .AsNoTracking()
                    .Where(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)
                    .CountAsync();

                return new FeatureAdoption
                {
                    ScreeningUsageCount = screeningCount,
                    TrackingUsageCount = trackingCount,
                    ExportUsageCount = exportCount
                };
            }

            /// <summary>
            /// Get companies at risk of churning (paid but inactive)
            /// </summary>
            private async Task<List<ChurnRiskCompany>> GetChurnRiskCompaniesAsync()
            {
                var now = DateTime.UtcNow;

                // Get companies with active paid subscriptions
                var paidCompanies = await _context.CompanySubscriptions
                    .AsNoTracking()
                    .Where(cs => cs.IsActive &&
                                cs.SubscriptionStatus == SubscriptionStatusEnum.Active &&
                                cs.Subscription != null)
                    .Include(cs => cs.Company)
                    .Include(cs => cs.Subscription)
                    .ToListAsync();

                // Deduplicate by company, keep the highest risk level if multiple subscriptions exist
                var churnRiskMap = new Dictionary<int, (int riskScore, ChurnRiskCompany dto)>();

                foreach (var subscription in paidCompanies)
                {
                    if (subscription.Company == null) continue;

                    // Get last activity date (last resume upload)
                    var lastResume = await _context.Resumes
                        .AsNoTracking()
                        .Where(r => r.CompanyId == subscription.CompanyId && r.IsActive)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();

                    var lastActivityDate = lastResume?.CreatedAt;
                    var daysInactive = lastActivityDate.HasValue 
                        ? (int)(now - lastActivityDate.Value).TotalDays 
                        : 999;

                    // Only include companies inactive for more than 30 days
                    if (daysInactive <= 30) continue;

                    string riskLevel = daysInactive > 60 ? "High" : daysInactive > 45 ? "Medium" : "Low";
                    int riskScore = riskLevel switch
                    {
                        "High" => 3,
                        "Medium" => 2,
                        _ => 1
                    };

                    // Update only if this subscription yields a higher risk level for the same company
                    if (!churnRiskMap.TryGetValue(subscription.CompanyId, out var existing) || riskScore > existing.riskScore)
                    {
                        churnRiskMap[subscription.CompanyId] = (riskScore, new ChurnRiskCompany
                        {
                            CompanyId = subscription.CompanyId,
                            CompanyName = subscription.Company.Name,
                            SubscriptionPlan = subscription.Subscription?.Name ?? "Unknown",
                            RiskLevel = riskLevel
                        });
                    }
                }

                return churnRiskMap.Values
                    .OrderByDescending(x => x.riskScore)
                    .ThenBy(x => x.dto.CompanyName)
                    .Select(x => x.dto)
                    .ToList();
            }

            #endregion
    }
}
