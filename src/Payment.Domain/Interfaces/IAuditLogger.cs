using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for logging and querying security events from audit logs.
/// Follows Interface Segregation Principle - focused on security event operations only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs a security event to the audit log.
    /// </summary>
    /// <param name="eventType">The type of security event.</param>
    /// <param name="userId">The user ID associated with the event (if applicable).</param>
    /// <param name="resource">The resource that was accessed.</param>
    /// <param name="action">The action that was performed.</param>
    /// <param name="succeeded">Whether the action succeeded.</param>
    /// <param name="details">Additional details about the event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogSecurityEventAsync(
        SecurityEventType eventType,
        string? userId,
        string resource,
        string action,
        bool succeeded,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries security events from the audit log.
    /// </summary>
    /// <param name="userId">Optional user ID to filter by.</param>
    /// <param name="eventType">Optional event type to filter by.</param>
    /// <param name="startDate">Optional start date for time range.</param>
    /// <param name="endDate">Optional end date for time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of security events matching the criteria.</returns>
    Task<IEnumerable<SecurityEvent>> QuerySecurityEventsAsync(
        string? userId = null,
        SecurityEventType? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
}

