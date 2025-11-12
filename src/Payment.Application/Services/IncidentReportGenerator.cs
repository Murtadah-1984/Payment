using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;

namespace Payment.Application.Services;

/// <summary>
/// Service for generating incident reports in various formats.
/// Follows Single Responsibility Principle - only responsible for report generation.
/// Implements Clean Architecture by depending on domain interfaces.
/// </summary>
public class IncidentReportGenerator : IIncidentReportGenerator
{
    private readonly ILogger<IncidentReportGenerator> _logger;

    public IncidentReportGenerator(ILogger<IncidentReportGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IncidentReport> GeneratePaymentFailureReportAsync(
        PaymentFailureIncident incident,
        ReportGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            options ??= new ReportGenerationOptions();
            var reportId = Guid.NewGuid().ToString();
            var markdown = GeneratePaymentFailureMarkdown(incident, options);
            var content = Encoding.UTF8.GetBytes(markdown);

            var report = new IncidentReport
            {
                ReportId = reportId,
                IncidentId = incident.IncidentId,
                IncidentType = "PaymentFailure",
                Severity = incident.Assessment.Severity.ToString(),
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = "System",
                Format = "Markdown",
                Content = content,
                Version = 1
            };
            return Task.FromResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payment failure report for incident {IncidentId}", incident.IncidentId);
            throw;
        }
    }

    public Task<IncidentReport> GenerateSecurityIncidentReportAsync(
        SecurityIncident incident,
        ReportGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            options ??= new ReportGenerationOptions();
            var reportId = Guid.NewGuid().ToString();
            var markdown = GenerateSecurityIncidentMarkdown(incident, options);
            var content = Encoding.UTF8.GetBytes(markdown);

            var report = new IncidentReport
            {
                ReportId = reportId,
                IncidentId = incident.IncidentId,
                IncidentType = "SecurityIncident",
                Severity = incident.Assessment.Severity.ToString(),
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = "System",
                Format = "Markdown",
                Content = content,
                Version = 1
            };
            return Task.FromResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security incident report for incident {IncidentId}", incident.IncidentId);
            throw;
        }
    }

    public Task<byte[]> ExportToMarkdownAsync(
        IncidentReport report,
        CancellationToken ct = default)
    {
        return Task.FromResult(report.Content);
    }

    public Task<byte[]> ExportToHtmlAsync(
        IncidentReport report,
        CancellationToken ct = default)
    {
        try
        {
            var markdown = Encoding.UTF8.GetString(report.Content);
            var html = ConvertMarkdownToHtml(markdown);
            return Task.FromResult(Encoding.UTF8.GetBytes(html));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting report to HTML");
            throw;
        }
    }

    public Task<byte[]> ExportToPdfAsync(
        IncidentReport report,
        CancellationToken ct = default)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            
            var markdown = Encoding.UTF8.GetString(report.Content);
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text($"Incident Report - {report.IncidentType}")
                        .SemiBold()
                        .FontSize(16)
                        .AlignCenter();

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(10);
                            
                            // Report metadata
                            column.Item().Text($"Incident ID: {report.IncidentId}");
                            column.Item().Text($"Severity: {report.Severity}");
                            column.Item().Text($"Generated At: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
                            column.Item().PaddingTop(10).Text("");

                            // Convert markdown sections to PDF
                            var sections = markdown.Split(new[] { "## " }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var section in sections)
                            {
                                if (string.IsNullOrWhiteSpace(section)) continue;
                                
                                var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length == 0) continue;

                                var title = lines[0].Trim();
                                column.Item().PaddingBottom(5).Text(title).SemiBold().FontSize(14);
                                
                                for (int i = 1; i < lines.Length; i++)
                                {
                                    var line = lines[i].Trim();
                                    if (string.IsNullOrWhiteSpace(line)) continue;
                                    
                                    // Remove markdown formatting
                                    line = line.Replace("**", "").Replace("*", "");
                                    
                                    if (line.StartsWith("- "))
                                    {
                                        column.Item().PaddingLeft(10).Text($"• {line.Substring(2)}");
                                    }
                                    else
                                    {
                                        column.Item().Text(line);
                                    }
                                }
                                
                                column.Item().PaddingTop(5).Text("");
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return Task.FromResult(stream.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF report");
            throw;
        }
    }

    private string GeneratePaymentFailureMarkdown(
        PaymentFailureIncident incident,
        ReportGenerationOptions options)
    {
        var sb = new StringBuilder();

        // Title
        sb.AppendLine($"# Payment Failure Incident Report");
        sb.AppendLine();
        sb.AppendLine($"**Incident ID:** {incident.IncidentId}");
        sb.AppendLine($"**Generated At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Severity:** {incident.Assessment.Severity}");
        sb.AppendLine();

        // Executive Summary
        if (options.IncludeExecutiveSummary)
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine($"A payment failure incident occurred starting at {incident.Context.StartTime:yyyy-MM-dd HH:mm:ss} UTC.");
            sb.AppendLine($"**Root Cause:** {incident.Assessment.RootCause}");
            sb.AppendLine($"**Affected Payments:** {incident.Assessment.AffectedPaymentCount}");
            sb.AppendLine($"**Affected Providers:** {string.Join(", ", incident.Assessment.AffectedProviders)}");
            if (incident.Context.EndTime.HasValue)
            {
                sb.AppendLine($"**Duration:** {incident.Context.Duration?.TotalMinutes:F2} minutes");
            }
            else
            {
                sb.AppendLine("**Status:** Ongoing");
            }
            sb.AppendLine();
        }

        // Incident Timeline
        if (options.IncludeTimeline && incident.Timeline.Any())
        {
            sb.AppendLine("## Incident Timeline");
            sb.AppendLine();
            foreach (var evt in incident.Timeline.OrderBy(e => e.Timestamp))
            {
                sb.AppendLine($"- **{evt.Timestamp:yyyy-MM-dd HH:mm:ss} UTC** - {evt.Event}");
                if (!string.IsNullOrEmpty(evt.Description))
                {
                    sb.AppendLine($"  - {evt.Description}");
                }
                if (!string.IsNullOrEmpty(evt.Actor))
                {
                    sb.AppendLine($"  - Actor: {evt.Actor}");
                }
            }
            sb.AppendLine();
        }

        // Root Cause Analysis
        if (options.IncludeRootCauseAnalysis)
        {
            sb.AppendLine("## Root Cause Analysis");
            sb.AppendLine();
            sb.AppendLine(incident.Assessment.RootCause);
            sb.AppendLine();
        }

        // Impact Assessment
        if (options.IncludeImpactAssessment)
        {
            sb.AppendLine("## Impact Assessment");
            sb.AppendLine();
            sb.AppendLine(incident.ImpactAssessment);
            sb.AppendLine($"- **Affected Payments:** {incident.Assessment.AffectedPaymentCount}");
            sb.AppendLine($"- **Estimated Resolution Time:** {incident.Assessment.EstimatedResolutionTime.TotalMinutes:F2} minutes");
            sb.AppendLine();
        }

        // Actions Taken
        if (options.IncludeActionsTaken && incident.ActionsTaken.Any())
        {
            sb.AppendLine("## Actions Taken");
            sb.AppendLine();
            foreach (var action in incident.ActionsTaken)
            {
                sb.AppendLine($"- {action}");
            }
            sb.AppendLine();
        }

        // Recommended Actions
        if (incident.Assessment.RecommendedActions.Any())
        {
            sb.AppendLine("## Recommended Actions");
            sb.AppendLine();
            foreach (var action in incident.Assessment.RecommendedActions)
            {
                var timeInfo = !string.IsNullOrEmpty(action.EstimatedTime) ? $" (Estimated: {action.EstimatedTime})" : "";
                sb.AppendLine($"- **{action.Priority}:** {action.Description}{timeInfo}");
            }
            sb.AppendLine();
        }

        // Preventive Measures
        if (options.IncludePreventiveMeasures && incident.PreventiveMeasures.Any())
        {
            sb.AppendLine("## Preventive Measures");
            sb.AppendLine();
            foreach (var measure in incident.PreventiveMeasures)
            {
                sb.AppendLine($"- {measure}");
            }
            sb.AppendLine();
        }

        // Lessons Learned
        if (options.IncludeLessonsLearned && incident.LessonsLearned.Any())
        {
            sb.AppendLine("## Lessons Learned");
            sb.AppendLine();
            foreach (var lesson in incident.LessonsLearned)
            {
                sb.AppendLine($"- {lesson}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateSecurityIncidentMarkdown(
        SecurityIncident incident,
        ReportGenerationOptions options)
    {
        var sb = new StringBuilder();

        // Title
        sb.AppendLine($"# Security Incident Report");
        sb.AppendLine();
        sb.AppendLine($"**Incident ID:** {incident.IncidentId}");
        sb.AppendLine($"**Generated At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Severity:** {incident.Assessment.Severity}");
        sb.AppendLine($"**Threat Type:** {incident.Assessment.ThreatType}");
        sb.AppendLine();

        // Executive Summary
        if (options.IncludeExecutiveSummary)
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine($"A security incident of type **{incident.Assessment.ThreatType}** was detected.");
            sb.AppendLine($"**Severity:** {incident.Assessment.Severity}");
            sb.AppendLine($"**Affected Resources:** {string.Join(", ", incident.Assessment.AffectedResources)}");
            if (incident.Assessment.CompromisedCredentials.Any())
            {
                sb.AppendLine($"**Compromised Credentials:** {incident.Assessment.CompromisedCredentials.Count()}");
            }
            sb.AppendLine();
        }

        // Incident Timeline
        if (options.IncludeTimeline && incident.Timeline.Any())
        {
            sb.AppendLine("## Incident Timeline");
            sb.AppendLine();
            foreach (var evt in incident.Timeline.OrderBy(e => e.Timestamp))
            {
                sb.AppendLine($"- **{evt.Timestamp:yyyy-MM-dd HH:mm:ss} UTC** - {evt.Event}");
                if (!string.IsNullOrEmpty(evt.Description))
                {
                    sb.AppendLine($"  - {evt.Description}");
                }
                if (!string.IsNullOrEmpty(evt.Actor))
                {
                    sb.AppendLine($"  - Actor: {evt.Actor}");
                }
            }
            sb.AppendLine();
        }

        // Impact Assessment
        if (options.IncludeImpactAssessment)
        {
            sb.AppendLine("## Impact Assessment");
            sb.AppendLine();
            sb.AppendLine(incident.ImpactAssessment);
            if (incident.AffectedUsers.Any())
            {
                sb.AppendLine($"- **Affected Users:** {incident.AffectedUsers.Count()}");
            }
            if (incident.CompromisedResources.Any())
            {
                sb.AppendLine($"- **Compromised Resources:** {string.Join(", ", incident.CompromisedResources)}");
            }
            sb.AppendLine();
        }

        // Actions Taken
        if (options.IncludeActionsTaken && incident.ActionsTaken.Any())
        {
            sb.AppendLine("## Actions Taken");
            sb.AppendLine();
            foreach (var action in incident.ActionsTaken)
            {
                sb.AppendLine($"- {action}");
            }
            sb.AppendLine();
        }

        // Remediation Actions
        if (incident.Assessment.RemediationActions.Any())
        {
            sb.AppendLine("## Remediation Actions");
            sb.AppendLine();
            foreach (var action in incident.Assessment.RemediationActions)
            {
                var timeInfo = !string.IsNullOrEmpty(action.EstimatedTime) ? $" (Estimated: {action.EstimatedTime})" : "";
                sb.AppendLine($"- **{action.Priority}:** {action.Description}{timeInfo}");
            }
            sb.AppendLine();
        }

        // Preventive Measures
        if (options.IncludePreventiveMeasures && incident.PreventiveMeasures.Any())
        {
            sb.AppendLine("## Preventive Measures");
            sb.AppendLine();
            foreach (var measure in incident.PreventiveMeasures)
            {
                sb.AppendLine($"- {measure}");
            }
            sb.AppendLine();
        }

        // Lessons Learned
        if (options.IncludeLessonsLearned && incident.LessonsLearned.Any())
        {
            sb.AppendLine("## Lessons Learned");
            sb.AppendLine();
            foreach (var lesson in incident.LessonsLearned)
            {
                sb.AppendLine($"- {lesson}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ConvertMarkdownToHtml(string markdown)
    {
        // Simple Markdown to HTML conversion
        // In production, use a library like Markdig
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
        html.AppendLine("h1 { color: #333; }");
        html.AppendLine("h2 { color: #666; margin-top: 30px; }");
        html.AppendLine("ul { line-height: 1.6; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("# "))
            {
                html.AppendLine($"<h1>{line.Substring(2)}</h1>");
            }
            else if (line.StartsWith("## "))
            {
                html.AppendLine($"<h2>{line.Substring(3)}</h2>");
            }
            else if (line.StartsWith("- "))
            {
                html.AppendLine($"<li>{line.Substring(2)}</li>");
            }
            else if (line.StartsWith("**") && line.EndsWith("**"))
            {
                html.AppendLine($"<strong>{line.Substring(2, line.Length - 4)}</strong>");
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                html.AppendLine($"<p>{line}</p>");
            }
            else
            {
                html.AppendLine("<br>");
            }
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }
}

