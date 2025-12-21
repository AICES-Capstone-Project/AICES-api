using BusinessObjectLayer.Common;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
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

        public ReportService(IUnitOfWork uow, AICESDbContext context, IHttpContextAccessor httpContextAccessor, IWebHostEnvironment webHostEnvironment)
        {
            _uow = uow;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _webHostEnvironment = webHostEnvironment;
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
                var totalCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .CountAsync();

                // 2. Active Companies (có ít nhất 1 job published hoặc có subscription active)
                var activeCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           (_context.Jobs.Any(j => j.CompanyId == c.CompanyId && j.IsActive && j.JobStatus == JobStatusEnum.Published) ||
                            _context.CompanySubscriptions.Any(cs => cs.CompanyId == c.CompanyId && cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active)))
                    .CountAsync();

                // 3. Total Jobs (all active jobs)
                var totalJobs = await _context.Jobs
                    .AsNoTracking()
                    .Where(j => j.IsActive)
                    .CountAsync();

                // 4. AI Processed Resumes (resumes with score)
                var aiProcessedResumes = await _context.ResumeApplications
                    .AsNoTracking()
                    .Where(ra => ra.IsActive && (ra.TotalScore != null || ra.AdjustedScore != null))
                    .Select(ra => ra.ResumeId)
                    .Distinct()
                    .CountAsync();

                // 5. Total Revenue (sum of successful payments via transactions)
                var totalRevenue = await _context.Transactions
                    .AsNoTracking()
                    .Where(t => t.IsActive && 
                           t.Payment != null && 
                           t.Payment.IsActive && 
                           t.Payment.PaymentStatus == PaymentStatusEnum.Paid)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                // 6. Company Retention Rate (companies that have renewed - have more than 1 subscription in history)
                // Đếm tất cả subscriptions (kể cả expired) để biết công ty có gia hạn hay không
                var companiesWithSubscriptions = await _context.CompanySubscriptions
                    .AsNoTracking()
                    .Select(cs => cs.CompanyId)
                    .Distinct()
                    .CountAsync();

                var companiesWithMultipleSubscriptions = await _context.CompanySubscriptions
                    .AsNoTracking()
                    .GroupBy(cs => cs.CompanyId)
                    .Where(g => g.Count() > 1) // Công ty có > 1 subscription record = đã gia hạn
                    .CountAsync();

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
                var totalCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .CountAsync();

                // 2. Active Companies (status = Approved)
                var activeCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                    .CountAsync();

                // 3. Inactive Companies (IsActive = true but not Approved)
                var inactiveCompanies = totalCompanies - activeCompanies;

                // 4. New Companies This Month
                var newCompaniesThisMonth = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CreatedAt >= firstDayOfMonth)
                    .CountAsync();

                // 5. Companies with Active Subscription
                var companiesWithActiveSubscription = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active))
                    .CountAsync();

                // 6. Companies with Expired Subscription (có subscription nhưng không có active)
                var companiesWithExpiredSubscription = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Expired) &&
                           !c.CompanySubscriptions.Any(cs => cs.IsActive && cs.SubscriptionStatus == SubscriptionStatusEnum.Active))
                    .CountAsync();

                // 7. Companies without any Subscription
                var companiesWithoutSubscription = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           !c.CompanySubscriptions.Any(cs => cs.IsActive))
                    .CountAsync();

                // 8. Verification Status Breakdown
                var verifiedCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                    .CountAsync();

                var pendingCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Pending)
                    .CountAsync();

                var rejectedCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Rejected)
                    .CountAsync();

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
                var totalCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.CompanyStatus == CompanyStatusEnum.Approved)
                    .CountAsync();

                // 1. Active Companies: Có ít nhất 1 job hoặc 1 resume
                var activeCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanyStatus == CompanyStatusEnum.Approved &&
                           (c.Jobs.Any(j => j.IsActive) || c.Resumes.Any(r => r.IsActive)))
                    .CountAsync();

                // 2. Registered Only: Đăng ký nhưng chưa tạo job hoặc resume nào
                var registeredOnly = totalCompanies - activeCompanies;

                // 3. Frequent Companies: Có hoạt động trong 7 ngày gần đây
                // (tạo job mới, upload resume, hoặc có resume application mới)
                var frequentCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanyStatus == CompanyStatusEnum.Approved &&
                           (c.Jobs.Any(j => j.IsActive && j.CreatedAt >= sevenDaysAgo) ||
                            c.Resumes.Any(r => r.IsActive && r.CreatedAt >= sevenDaysAgo) ||
                            c.Jobs.Any(j => j.IsActive && j.ResumeApplications.Any(ra => ra.IsActive && ra.CreatedAt >= sevenDaysAgo))))
                    .CountAsync();

                // KPIs
                // 4. Active Rate: % công ty active / tổng công ty
                decimal activeRate = totalCompanies > 0 
                    ? Math.Round((decimal)activeCompanies / totalCompanies, 2)
                    : 0;

                // 5. AI Usage Rate: % công ty có dùng AI screening (có resume được score)
                var companiesUsingAI = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanyStatus == CompanyStatusEnum.Approved &&
                           c.Resumes.Any(r => r.IsActive && 
                                        r.ResumeApplications.Any(ra => ra.IsActive && 
                                                                (ra.TotalScore != null || ra.AdjustedScore != null))))
                    .CountAsync();

                decimal aiUsageRate = totalCompanies > 0 
                    ? Math.Round((decimal)companiesUsingAI / totalCompanies, 2)
                    : 0;

                // 6. Returning Rate: % công ty có hoạt động trong 30 ngày gần đây
                var returningCompanies = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsActive && 
                           c.CompanyStatus == CompanyStatusEnum.Approved &&
                           (c.Jobs.Any(j => j.IsActive && j.CreatedAt >= thirtyDaysAgo) ||
                            c.Resumes.Any(r => r.IsActive && r.CreatedAt >= thirtyDaysAgo) ||
                            c.Jobs.Any(j => j.IsActive && j.ResumeApplications.Any(ra => ra.IsActive && ra.CreatedAt >= thirtyDaysAgo))))
                    .CountAsync();

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
    }
}
