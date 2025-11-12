using Payment.Application.DTOs;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for querying audit logs with advanced filtering and pagination.
/// Follows Interface Segregation Principle - focused interface for audit log queries.
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    /// Query audit logs with filtering, pagination, and sorting.
    /// </summary>
    Task<AuditLogQueryResult> QueryAsync(
        AuditLogQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Get summary statistics for a time range.
    /// </summary>
    Task<AuditLogSummary> GetSummaryAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct = default);

    /// <summary>
    /// Get security events with filtering.
    /// </summary>
    Task<IEnumerable<SecurityEventDto>> GetSecurityEventsAsync(
        SecurityEventQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Export audit logs to CSV format.
    /// </summary>
    Task<byte[]> ExportToCsvAsync(
        AuditLogQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Export audit logs to JSON format.
    /// </summary>
    Task<byte[]> ExportToJsonAsync(
        AuditLogQuery query,
        CancellationToken ct = default);
}

