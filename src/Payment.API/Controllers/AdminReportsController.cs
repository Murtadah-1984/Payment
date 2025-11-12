using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Interfaces;

namespace Payment.API.Controllers;

/// <summary>
/// Admin-only reporting controller for enterprise-grade financial visibility.
/// Requires SystemOwner role for access.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/reports")]
[ApiVersion("1.0")]
[Authorize(Roles = "SystemOwner")]
public class AdminReportsController : ControllerBase
{
    private readonly IPaymentReportingService _reportingService;
    private readonly IPaymentReportingScheduler _reportingScheduler;
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(
        IPaymentReportingService reportingService,
        IPaymentReportingScheduler reportingScheduler,
        ILogger<AdminReportsController> logger)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _reportingScheduler = reportingScheduler ?? throw new ArgumentNullException(nameof(reportingScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get monthly report with comprehensive financial aggregations.
    /// </summary>
    [HttpGet("monthly")]
    [ProducesResponseType(typeof(MonthlyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthly(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Monthly report requested: Year={Year}, Month={Month}, ProjectCode={ProjectCode}", year, month, projectCode);

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest("Invalid year or month");
        }

        var report = await _reportingService.GetMonthlyReportAsync(year, month, projectCode, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Get merchant-specific report.
    /// </summary>
    [HttpGet("merchant/{merchantId}")]
    [ProducesResponseType(typeof(MerchantReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMerchantReport(
        string merchantId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Merchant report requested: MerchantId={MerchantId}, From={From}, To={To}", merchantId, from, to);

        if (string.IsNullOrWhiteSpace(merchantId))
        {
            return BadRequest("MerchantId is required");
        }

        if (from >= to)
        {
            return BadRequest("From date must be before To date");
        }

        var report = await _reportingService.GetMerchantReportAsync(merchantId, from, to, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Get provider-specific report.
    /// </summary>
    [HttpGet("provider/{provider}")]
    [ProducesResponseType(typeof(ProviderReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderReport(
        string provider,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Provider report requested: Provider={Provider}, From={From}, To={To}", provider, from, to);

        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest("Provider is required");
        }

        if (from >= to)
        {
            return BadRequest("From date must be before To date");
        }

        var report = await _reportingService.GetProviderReportAsync(provider, from, to, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Get refund report.
    /// </summary>
    [HttpGet("refunds")]
    [ProducesResponseType(typeof(RefundReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefundReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refund report requested: From={From}, To={To}", from, to);

        if (from >= to)
        {
            return BadRequest("From date must be before To date");
        }

        var report = await _reportingService.GetRefundReportAsync(from, to, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Get complete dispute trace for a payment (forensic-level audit trail).
    /// </summary>
    [HttpGet("dispute/{paymentId:guid}")]
    [ProducesResponseType(typeof(DisputeTraceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDisputeTrace(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dispute trace requested: PaymentId={PaymentId}", paymentId);

        try
        {
            var trace = await _reportingService.GetDisputeTraceAsync(paymentId, cancellationToken);
            return Ok(trace);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Payment with ID {paymentId} not found");
        }
    }

    /// <summary>
    /// Get real-time statistics.
    /// </summary>
    [HttpGet("realtime")]
    [ProducesResponseType(typeof(RealTimeStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRealTimeStats(
        [FromQuery] string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Real-time stats requested: ProjectCode={ProjectCode}", projectCode);

        var stats = await _reportingService.GetRealTimeStatsAsync(projectCode, cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Get revenue forecast.
    /// </summary>
    [HttpGet("forecast")]
    [ProducesResponseType(typeof(ForecastReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForecast(
        [FromQuery] string? projectCode = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Forecast requested: ProjectCode={ProjectCode}, From={From}, To={To}", projectCode, from, to);

        var fromDate = from ?? DateTime.UtcNow;
        var toDate = to ?? DateTime.UtcNow.AddMonths(3);

        if (fromDate >= toDate)
        {
            return BadRequest("From date must be before To date");
        }

        var forecast = await _reportingService.GetRevenueForecastAsync(projectCode, fromDate, toDate, cancellationToken);
        return Ok(forecast);
    }

    /// <summary>
    /// Get anomaly alerts.
    /// </summary>
    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(IReadOnlyList<AnomalyAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAnomalies(
        [FromQuery] string? projectCode = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Anomalies requested: ProjectCode={ProjectCode}, From={From}, To={To}", projectCode, from, to);

        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;

        if (fromDate >= toDate)
        {
            return BadRequest("From date must be before To date");
        }

        var anomalies = await _reportingService.GetAnomaliesAsync(projectCode, fromDate, toDate, cancellationToken);
        return Ok(anomalies);
    }

    /// <summary>
    /// Manually trigger monthly report generation.
    /// Generates PDF and CSV reports, uploads to storage, and publishes event to Notification Microservice.
    /// </summary>
    [HttpPost("monthly/generate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateMonthlyReport(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual monthly report generation requested: Year={Year}, Month={Month}", year, month);

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest(new { Message = "Invalid year or month. Year must be between 2000-2100, month must be 1-12." });
        }

        try
        {
            var reportMonth = new DateTime(year, month, 1);
            var (reportId, reportUrl, pdfUrl, csvUrl) = await _reportingScheduler.GenerateMonthlyReportAsync(reportMonth, cancellationToken);

            return Ok(new
            {
                Message = "Report generation triggered successfully.",
                ReportId = reportId,
                ReportUrl = reportUrl,
                PdfUrl = pdfUrl,
                CsvUrl = csvUrl,
                Year = year,
                Month = month
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly report for {Year}-{Month:D2}", year, month);
            return StatusCode(500, new { Message = "Failed to generate report. Please check logs for details." });
        }
    }
}

