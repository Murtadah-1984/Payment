using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.API.Controllers.Admin;

/// <summary>
/// Admin API Controller for payment failure incident management.
/// Follows Clean Architecture - thin controller that delegates to Application layer.
/// Requires AdminOnly authorization policy.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/incidents")]
[ApiVersion("1.0")]
[Authorize(Policy = "AdminOnly")]
public class IncidentController : ControllerBase
{
    private readonly IIncidentResponseService _incidentResponseService;
    private readonly IIncidentReportGenerator _reportGenerator;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(
        IIncidentResponseService incidentResponseService,
        IIncidentReportGenerator reportGenerator,
        ILogger<IncidentController> logger)
    {
        _incidentResponseService = incidentResponseService ?? throw new ArgumentNullException(nameof(incidentResponseService));
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Assesses a payment failure incident and provides recommendations.
    /// </summary>
    /// <param name="request">The payment failure assessment request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An assessment of the incident with recommended actions.</returns>
    [HttpPost("payment-failure/assess")]
    [ProducesResponseType(typeof(IncidentAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IncidentAssessment>> AssessPaymentFailure(
        [FromBody] AssessPaymentFailureRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Assessing payment failure incident. Provider: {Provider}, FailureType: {FailureType}",
            request.Provider, request.FailureType);

        try
        {
            // In production, query actual affected payment count from repository
            var affectedPaymentCount = 0; // Placeholder - would query from payment repository
            
            var context = request.ToContext(affectedPaymentCount);
            var assessment = await _incidentResponseService.AssessPaymentFailureAsync(context, cancellationToken);

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing payment failure incident");
            return BadRequest(new { error = "Failed to assess payment failure incident", message = ex.Message });
        }
    }

    /// <summary>
    /// Processes refunds for affected payments.
    /// </summary>
    /// <param name="request">The refund request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refund operation.</returns>
    [HttpPost("payment-failure/refund")]
    [ProducesResponseType(typeof(RefundResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RefundResult>> ProcessRefunds(
        [FromBody] ProcessRefundsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing refunds for {Count} payments", request.PaymentIds.Count());

        try
        {
            var paymentIds = request.ToPaymentIds();
            var refundStatuses = await _incidentResponseService.ProcessAutomaticRefundsAsync(paymentIds, cancellationToken);
            
            var result = RefundResult.Create(refundStatuses);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refunds");
            return BadRequest(new { error = "Failed to process refunds", message = ex.Message });
        }
    }

    /// <summary>
    /// Resets the circuit breaker for a payment provider.
    /// </summary>
    /// <param name="provider">The payment provider name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("circuit-breaker/reset/{provider}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetCircuitBreaker(
        [FromRoute] string provider,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Resetting circuit breaker for provider: {Provider}", provider);

        try
        {
            // In production, this would call ICircuitBreakerService.ResetAsync
            // For now, we'll just log the action
            _logger.LogWarning("Circuit breaker reset not fully implemented. Provider: {Provider}", provider);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting circuit breaker for provider: {Provider}", provider);
            return BadRequest(new { error = "Failed to reset circuit breaker", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets incident metrics for a specified time range.
    /// </summary>
    /// <param name="startDate">Start date for metrics (optional, defaults to 24 hours ago).</param>
    /// <param name="endDate">End date for metrics (optional, defaults to now).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Incident metrics.</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(IncidentMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IncidentMetricsDto>> GetIncidentMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting incident metrics. StartDate: {StartDate}, EndDate: {EndDate}",
            startDate, endDate);

        var start = startDate ?? DateTime.UtcNow.AddHours(-24);
        var end = endDate ?? DateTime.UtcNow;
        
        var timeRange = new TimeRange(start, end);
        var metrics = await _incidentResponseService.GetIncidentMetricsAsync(timeRange, cancellationToken);

        return Ok(metrics);
    }

    /// <summary>
    /// Generate a payment failure incident report.
    /// </summary>
    /// <param name="incident">The payment failure incident data.</param>
    /// <param name="format">Report format (markdown, html, pdf). Default is markdown.</param>
    /// <param name="options">Report generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated incident report.</returns>
    [HttpPost("payment-failure/report")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GeneratePaymentFailureReport(
        [FromBody] PaymentFailureIncident incident,
        [FromQuery] string format = "markdown",
        [FromQuery] bool includeExecutiveSummary = true,
        [FromQuery] bool includeTimeline = true,
        [FromQuery] bool includeRootCauseAnalysis = true,
        [FromQuery] bool includeImpactAssessment = true,
        [FromQuery] bool includeActionsTaken = true,
        [FromQuery] bool includePreventiveMeasures = true,
        [FromQuery] bool includeLessonsLearned = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating payment failure report for incident {IncidentId}", incident.IncidentId);

        try
        {
            var options = new ReportGenerationOptions
            {
                IncludeExecutiveSummary = includeExecutiveSummary,
                IncludeTimeline = includeTimeline,
                IncludeRootCauseAnalysis = includeRootCauseAnalysis,
                IncludeImpactAssessment = includeImpactAssessment,
                IncludeActionsTaken = includeActionsTaken,
                IncludePreventiveMeasures = includePreventiveMeasures,
                IncludeLessonsLearned = includeLessonsLearned
            };
            
            var report = await _reportGenerator.GeneratePaymentFailureReportAsync(incident, options, cancellationToken);

            return format.ToLower() switch
            {
                "html" => File(await _reportGenerator.ExportToHtmlAsync(report, cancellationToken), "text/html", $"incident-{report.IncidentId}.html"),
                "pdf" => File(await _reportGenerator.ExportToPdfAsync(report, cancellationToken), "application/pdf", $"incident-{report.IncidentId}.pdf"),
                _ => File(report.Content, "text/markdown", $"incident-{report.IncidentId}.md")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payment failure report");
            return BadRequest(new { error = "Failed to generate report", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate a security incident report.
    /// </summary>
    /// <param name="incident">The security incident data.</param>
    /// <param name="format">Report format (markdown, html, pdf). Default is markdown.</param>
    /// <param name="options">Report generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated incident report.</returns>
    [HttpPost("security/report")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateSecurityIncidentReport(
        [FromBody] SecurityIncident incident,
        [FromQuery] string format = "markdown",
        [FromQuery] bool includeExecutiveSummary = true,
        [FromQuery] bool includeTimeline = true,
        [FromQuery] bool includeRootCauseAnalysis = true,
        [FromQuery] bool includeImpactAssessment = true,
        [FromQuery] bool includeActionsTaken = true,
        [FromQuery] bool includePreventiveMeasures = true,
        [FromQuery] bool includeLessonsLearned = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating security incident report for incident {IncidentId}", incident.IncidentId);

        try
        {
            var options = new ReportGenerationOptions
            {
                IncludeExecutiveSummary = includeExecutiveSummary,
                IncludeTimeline = includeTimeline,
                IncludeRootCauseAnalysis = includeRootCauseAnalysis,
                IncludeImpactAssessment = includeImpactAssessment,
                IncludeActionsTaken = includeActionsTaken,
                IncludePreventiveMeasures = includePreventiveMeasures,
                IncludeLessonsLearned = includeLessonsLearned
            };
            
            var report = await _reportGenerator.GenerateSecurityIncidentReportAsync(incident, options, cancellationToken);

            return format.ToLower() switch
            {
                "html" => File(await _reportGenerator.ExportToHtmlAsync(report, cancellationToken), "text/html", $"security-incident-{report.IncidentId}.html"),
                "pdf" => File(await _reportGenerator.ExportToPdfAsync(report, cancellationToken), "application/pdf", $"security-incident-{report.IncidentId}.pdf"),
                _ => File(report.Content, "text/markdown", $"security-incident-{report.IncidentId}.md")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security incident report");
            return BadRequest(new { error = "Failed to generate report", message = ex.Message });
        }
    }
}

