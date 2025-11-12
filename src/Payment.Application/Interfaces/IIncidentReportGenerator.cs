using Payment.Application.DTOs;

namespace Payment.Application.Interfaces;

/// <summary>
/// Interface for generating incident reports in various formats.
/// Follows Interface Segregation Principle - focused interface for report generation.
/// </summary>
public interface IIncidentReportGenerator
{
    /// <summary>
    /// Generate a payment failure incident report.
    /// </summary>
    Task<IncidentReport> GeneratePaymentFailureReportAsync(
        PaymentFailureIncident incident,
        ReportGenerationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a security incident report.
    /// </summary>
    Task<IncidentReport> GenerateSecurityIncidentReportAsync(
        SecurityIncident incident,
        ReportGenerationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Export report to Markdown format.
    /// </summary>
    Task<byte[]> ExportToMarkdownAsync(
        IncidentReport report,
        CancellationToken ct = default);

    /// <summary>
    /// Export report to HTML format.
    /// </summary>
    Task<byte[]> ExportToHtmlAsync(
        IncidentReport report,
        CancellationToken ct = default);

    /// <summary>
    /// Export report to PDF format.
    /// </summary>
    Task<byte[]> ExportToPdfAsync(
        IncidentReport report,
        CancellationToken ct = default);
}

