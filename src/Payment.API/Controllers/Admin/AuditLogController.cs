using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Domain.Interfaces;

namespace Payment.API.Controllers.Admin;

/// <summary>
/// Admin controller for querying audit logs.
/// Requires SecurityAdminOnly authorization policy for compliance and security.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/audit-logs")]
[ApiVersion("1.0")]
[Authorize(Policy = "SecurityAdminOnly")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly ILogger<AuditLogController> _logger;

    public AuditLogController(
        IAuditLogQueryService auditLogQueryService,
        ILogger<AuditLogController> logger)
    {
        _auditLogQueryService = auditLogQueryService ?? throw new ArgumentNullException(nameof(auditLogQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Query audit logs with filtering, pagination, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryAuditLogs(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Audit log query requested: UserId={UserId}, EventType={EventType}, Page={Page}",
            query.UserId, query.EventType, query.Page);

        try
        {
            var result = await _auditLogQueryService.QueryAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit logs");
            return StatusCode(500, new { Message = "An error occurred while querying audit logs." });
        }
    }

    /// <summary>
    /// Get audit log summary statistics for a time range.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AuditLogSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Audit log summary requested: StartTime={StartTime}, EndTime={EndTime}",
            startTime, endTime);

        if (startTime >= endTime)
        {
            return BadRequest(new { Message = "StartTime must be before EndTime." });
        }

        if ((endTime - startTime).TotalDays > 365)
        {
            return BadRequest(new { Message = "Time range cannot exceed 365 days." });
        }

        try
        {
            var summary = await _auditLogQueryService.GetSummaryAsync(startTime, endTime, cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log summary");
            return StatusCode(500, new { Message = "An error occurred while getting audit log summary." });
        }
    }

    /// <summary>
    /// Get security events with filtering.
    /// </summary>
    [HttpGet("security-events")]
    [ProducesResponseType(typeof(IEnumerable<SecurityEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSecurityEvents(
        [FromQuery] SecurityEventQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Security events query requested: UserId={UserId}, EventType={EventType}",
            query.UserId, query.EventType);

        try
        {
            var events = await _auditLogQueryService.GetSecurityEventsAsync(query, cancellationToken);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying security events");
            return StatusCode(500, new { Message = "An error occurred while querying security events." });
        }
    }

    /// <summary>
    /// Export audit logs to CSV format.
    /// </summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportToCsv(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Audit log CSV export requested");

        try
        {
            var csvData = await _auditLogQueryService.ExportToCsvAsync(query, cancellationToken);
            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(csvData, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            return StatusCode(500, new { Message = "An error occurred while exporting audit logs." });
        }
    }

    /// <summary>
    /// Export audit logs to JSON format.
    /// </summary>
    [HttpGet("export/json")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportToJson(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Audit log JSON export requested");

        try
        {
            var jsonData = await _auditLogQueryService.ExportToJsonAsync(query, cancellationToken);
            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            return File(jsonData, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to JSON");
            return StatusCode(500, new { Message = "An error occurred while exporting audit logs." });
        }
    }
}

