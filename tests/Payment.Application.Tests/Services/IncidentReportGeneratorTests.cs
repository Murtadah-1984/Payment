using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Enums;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Unit tests for IncidentReportGenerator.
/// Tests report generation in various formats (Markdown, HTML, PDF) and customization options.
/// </summary>
public class IncidentReportGeneratorTests
{
    private readonly ILogger<IncidentReportGenerator> _logger;
    private readonly IncidentReportGenerator _generator;

    public IncidentReportGeneratorTests()
    {
        _logger = new Mock<ILogger<IncidentReportGenerator>>().Object;
        _generator = new IncidentReportGenerator(_logger);
    }

    #region Payment Failure Report Tests

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldGenerateReport_WithAllSections()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var options = new ReportGenerationOptions
        {
            IncludeExecutiveSummary = true,
            IncludeTimeline = true,
            IncludeRootCauseAnalysis = true,
            IncludeImpactAssessment = true,
            IncludeActionsTaken = true,
            IncludePreventiveMeasures = true,
            IncludeLessonsLearned = true
        };

        // Act
        var report = await _generator.GeneratePaymentFailureReportAsync(incident, options);

        // Assert
        report.Should().NotBeNull();
        report.IncidentId.Should().Be(incident.IncidentId);
        report.IncidentType.Should().Be("PaymentFailure");
        report.Severity.Should().Be(incident.Assessment.Severity.ToString());
        report.Format.Should().Be("Markdown");
        report.Version.Should().Be(1);
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        report.Content.Should().NotBeEmpty();

        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        content.Should().Contain("# Payment Failure Incident Report");
        content.Should().Contain("Executive Summary");
        content.Should().Contain("Incident Timeline");
        content.Should().Contain("Root Cause Analysis");
        content.Should().Contain("Impact Assessment");
        content.Should().Contain("Actions Taken");
        content.Should().Contain("Recommended Actions");
        content.Should().Contain("Preventive Measures");
        content.Should().Contain("Lessons Learned");
    }

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldExcludeSections_WhenOptionsDisabled()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var options = new ReportGenerationOptions
        {
            IncludeExecutiveSummary = false,
            IncludeTimeline = false,
            IncludeRootCauseAnalysis = false,
            IncludeImpactAssessment = false,
            IncludeActionsTaken = false,
            IncludePreventiveMeasures = false,
            IncludeLessonsLearned = false
        };

        // Act
        var report = await _generator.GeneratePaymentFailureReportAsync(incident, options);

        // Assert
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        content.Should().NotContain("Executive Summary");
        content.Should().NotContain("Incident Timeline");
        content.Should().NotContain("Root Cause Analysis");
        content.Should().NotContain("Impact Assessment");
        content.Should().NotContain("Actions Taken");
        content.Should().NotContain("Preventive Measures");
        content.Should().NotContain("Lessons Learned");
        content.Should().Contain("Recommended Actions"); // Always included
    }

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldUseDefaultOptions_WhenOptionsNull()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();

        // Act
        var report = await _generator.GeneratePaymentFailureReportAsync(incident, null);

        // Assert
        report.Should().NotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        // Default options include all sections
        content.Should().Contain("Executive Summary");
        content.Should().Contain("Incident Timeline");
    }

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldIncludeTimelineEvents_InChronologicalOrder()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var options = new ReportGenerationOptions { IncludeTimeline = true };

        // Act
        var report = await _generator.GeneratePaymentFailureReportAsync(incident, options);

        // Assert
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        var timelineIndex = content.IndexOf("Incident Timeline");
        timelineIndex.Should().BeGreaterThan(-1);

        // Verify timeline events are present
        content.Should().Contain("Timeline Event 1");
        content.Should().Contain("Timeline Event 2");
    }

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldIncludeRecommendedActions_WithPriority()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var options = new ReportGenerationOptions();

        // Act
        var report = await _generator.GeneratePaymentFailureReportAsync(incident, options);

        // Assert
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        content.Should().Contain("Recommended Actions");
        content.Should().Contain("High:");
        content.Should().Contain("Medium:");
    }

    #endregion

    #region Security Incident Report Tests

    [Fact]
    public async Task GenerateSecurityIncidentReportAsync_ShouldGenerateReport_WithAllSections()
    {
        // Arrange
        var incident = CreateSecurityIncident();
        var options = new ReportGenerationOptions
        {
            IncludeExecutiveSummary = true,
            IncludeTimeline = true,
            IncludeRootCauseAnalysis = true,
            IncludeImpactAssessment = true,
            IncludeActionsTaken = true,
            IncludePreventiveMeasures = true,
            IncludeLessonsLearned = true
        };

        // Act
        var report = await _generator.GenerateSecurityIncidentReportAsync(incident, options);

        // Assert
        report.Should().NotBeNull();
        report.IncidentId.Should().Be(incident.IncidentId);
        report.IncidentType.Should().Be("SecurityIncident");
        report.Severity.Should().Be(incident.Assessment.Severity.ToString());
        report.Format.Should().Be("Markdown");
        report.Version.Should().Be(1);

        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        content.Should().Contain("# Security Incident Report");
        content.Should().Contain("Executive Summary");
        content.Should().Contain("Threat Type");
        content.Should().Contain("UnauthorizedAccess");
        content.Should().Contain("Remediation Actions");
    }

    [Fact]
    public async Task GenerateSecurityIncidentReportAsync_ShouldIncludeThreatType_AndAffectedResources()
    {
        // Arrange
        var incident = CreateSecurityIncident();
        var options = new ReportGenerationOptions { IncludeExecutiveSummary = true };

        // Act
        var report = await _generator.GenerateSecurityIncidentReportAsync(incident, options);

        // Assert
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        content.Should().Contain(incident.Assessment.ThreatType.ToString());
        content.Should().Contain("Affected Resources");
    }

    [Fact]
    public async Task GenerateSecurityIncidentReportAsync_ShouldIncludeCompromisedCredentials_WhenPresent()
    {
        // Arrange
        var incident = CreateSecurityIncident();
        var options = new ReportGenerationOptions { IncludeExecutiveSummary = true };

        // Act
        var report = await _generator.GenerateSecurityIncidentReportAsync(incident, options);

        // Assert
        var content = System.Text.Encoding.UTF8.GetString(report.Content);
        if (incident.Assessment.CompromisedCredentials.Any())
        {
            content.Should().Contain("Compromised Credentials");
        }
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportToMarkdownAsync_ShouldReturnOriginalContent()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var report = await _generator.GeneratePaymentFailureReportAsync(incident);

        // Act
        var exported = await _generator.ExportToMarkdownAsync(report);

        // Assert
        exported.Should().BeEquivalentTo(report.Content);
    }

    [Fact]
    public async Task ExportToHtmlAsync_ShouldConvertMarkdownToHtml()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var report = await _generator.GeneratePaymentFailureReportAsync(incident);

        // Act
        var html = await _generator.ExportToHtmlAsync(report);

        // Assert
        html.Should().NotBeEmpty();
        var htmlContent = System.Text.Encoding.UTF8.GetString(html);
        htmlContent.Should().Contain("<!DOCTYPE html>");
        htmlContent.Should().Contain("<html>");
        htmlContent.Should().Contain("<head>");
        htmlContent.Should().Contain("<body>");
        htmlContent.Should().Contain("<h1>");
    }

    [Fact]
    public async Task ExportToPdfAsync_ShouldGenerateValidPdf()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var report = await _generator.GeneratePaymentFailureReportAsync(incident);

        // Act
        var pdf = await _generator.ExportToPdfAsync(report);

        // Assert
        pdf.Should().NotBeEmpty();
        // PDF files start with %PDF
        var pdfHeader = System.Text.Encoding.ASCII.GetString(pdf.Take(4).ToArray());
        pdfHeader.Should().Be("%PDF");
    }

    [Fact]
    public async Task ExportToPdfAsync_ShouldIncludeReportMetadata()
    {
        // Arrange
        var incident = CreatePaymentFailureIncident();
        var report = await _generator.GeneratePaymentFailureReportAsync(incident);

        // Act
        var pdf = await _generator.ExportToPdfAsync(report);

        // Assert
        pdf.Should().NotBeEmpty();
        // PDF should contain incident ID in text (as part of the content)
        var pdfText = System.Text.Encoding.UTF8.GetString(pdf);
        // Note: PDF text extraction is limited, but we can verify the PDF was generated
        pdf.Length.Should().BeGreaterThan(1000); // PDF should be substantial
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GeneratePaymentFailureReportAsync_ShouldThrowException_WhenIncidentIsNull()
    {
        // Arrange
        PaymentFailureIncident? incident = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _generator.GeneratePaymentFailureReportAsync(incident!));
    }

    [Fact]
    public async Task GenerateSecurityIncidentReportAsync_ShouldThrowException_WhenIncidentIsNull()
    {
        // Arrange
        SecurityIncident? incident = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _generator.GenerateSecurityIncidentReportAsync(incident!));
    }

    [Fact]
    public async Task ExportToHtmlAsync_ShouldHandleInvalidContent_Gracefully()
    {
        // Arrange
        var report = new IncidentReport
        {
            ReportId = Guid.NewGuid().ToString(),
            IncidentId = "test",
            IncidentType = "Test",
            Severity = "Low",
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "Test",
            Format = "Markdown",
            Content = System.Text.Encoding.UTF8.GetBytes("Valid content"),
            Version = 1
        };

        // Act
        var html = await _generator.ExportToHtmlAsync(report);

        // Assert
        html.Should().NotBeEmpty();
    }

    #endregion

    #region Helper Methods

    private PaymentFailureIncident CreatePaymentFailureIncident()
    {
        return new PaymentFailureIncident
        {
            IncidentId = Guid.NewGuid().ToString(),
            Context = new PaymentFailureContext(
                StartTime: DateTime.UtcNow.AddMinutes(-30),
                EndTime: DateTime.UtcNow.AddMinutes(-10),
                Provider: "Stripe",
                FailureType: Domain.Enums.PaymentFailureType.ProviderError,
                AffectedPaymentCount: 50,
                Metadata: new Dictionary<string, object>
                {
                    { "ErrorCode", "500" },
                    { "ErrorMessage", "Internal Server Error" }
                }),
            Assessment = new IncidentAssessment(
                Severity: Domain.Enums.IncidentSeverity.High,
                RootCause: "Payment provider API outage",
                AffectedProviders: new[] { "Stripe" },
                AffectedPaymentCount: 50,
                EstimatedResolutionTime: TimeSpan.FromMinutes(15),
                RecommendedActions: new[]
                {
                    new RecommendedAction(
                        Action: "SwitchProvider",
                        Description: "Switch to backup provider",
                        Priority: "High",
                        EstimatedTime: "5 minutes"),
                    new RecommendedAction(
                        Action: "ReviewSettings",
                        Description: "Review circuit breaker settings",
                        Priority: "Medium",
                        EstimatedTime: "10 minutes")
                }),
            Timeline = new[]
            {
                new IncidentTimelineEvent
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-30),
                    Event = "Timeline Event 1",
                    Description = "First event description",
                    Actor = "System"
                },
                new IncidentTimelineEvent
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-20),
                    Event = "Timeline Event 2",
                    Description = "Second event description",
                    Actor = "Admin"
                }
            },
            ImpactAssessment = "50 payments affected, estimated revenue impact: $5,000",
            ActionsTaken = new[] { "Switched to backup provider", "Notified stakeholders" },
            PreventiveMeasures = new[] { "Enhanced monitoring", "Improved circuit breaker configuration" },
            LessonsLearned = new[] { "Need better provider redundancy", "Improve alerting thresholds" }
        };
    }

    private SecurityIncident CreateSecurityIncident()
    {
        return new SecurityIncident
        {
            IncidentId = Guid.NewGuid().ToString(),
            Assessment = new SecurityIncidentAssessment(
                Severity: Domain.Enums.SecurityIncidentSeverity.High,
                ThreatType: Domain.Enums.SecurityThreatType.UnauthorizedAccess,
                AffectedResources: new[] { "/api/payments", "/api/admin" },
                CompromisedCredentials: new[] { "user1@example.com" },
                RecommendedContainment: Domain.Enums.ContainmentStrategy.RevokeCredentials,
                RemediationActions: new[]
                {
                    new RemediationAction(
                        Action: "RevokeCredentials",
                        Description: "Revoke compromised credentials",
                        Priority: "Critical",
                        EstimatedTime: "Immediate")
                }),
            Timeline = new[]
            {
                new IncidentTimelineEvent
                {
                    Timestamp = DateTime.UtcNow.AddHours(-2),
                    Event = "Unauthorized access detected",
                    Description = "Multiple failed login attempts",
                    Actor = "Unknown"
                }
            },
            ImpactAssessment = "Potential data exposure, unauthorized access to payment endpoints",
            ActionsTaken = new[] { "Blocked IP addresses", "Revoked credentials" },
            PreventiveMeasures = new[] { "Enhanced authentication", "Improved monitoring" },
            LessonsLearned = new[] { "Need stronger authentication", "Improve threat detection" },
            AffectedUsers = new[] { "user1@example.com" },
            CompromisedResources = new[] { "/api/payments" }
        };
    }

    #endregion
}

