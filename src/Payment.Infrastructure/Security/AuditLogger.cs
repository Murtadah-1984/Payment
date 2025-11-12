using Microsoft.Extensions.Logging;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Security;

/// <summary>
/// Service for logging and querying security events from audit logs.
/// Follows Single Responsibility Principle - only handles security event logging and querying.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IAuditLogRepository auditLogRepository,
        ILogger<AuditLogger> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogSecurityEventAsync(
        SecurityEventType eventType,
        string? userId,
        string resource,
        string action,
        bool succeeded,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resource))
            throw new ArgumentException("Resource cannot be null or empty", nameof(resource));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be null or empty", nameof(action));

        _logger.LogInformation(
            "Logging security event. EventType: {EventType}, UserId: {UserId}, Resource: {Resource}, Action: {Action}, Succeeded: {Succeeded}",
            eventType, userId, resource, action, succeeded);

        // In production, this would create an AuditLog entity and save it
        // For now, we'll use the existing audit log repository
        // Note: The AuditLog entity doesn't have a SecurityEventType field, so we'd need to extend it
        // or create a separate SecurityEventLog entity
        
        // For now, we'll just log to the application logger
        // In production, implement proper audit log persistence
        _logger.LogWarning(
            "Security event logging to database not fully implemented. EventType: {EventType}, Resource: {Resource}",
            eventType, resource);
    }

    public async Task<IEnumerable<SecurityEvent>> QuerySecurityEventsAsync(
        string? userId = null,
        SecurityEventType? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying security events. UserId: {UserId}, EventType: {EventType}, StartDate: {StartDate}, EndDate: {EndDate}",
            userId, eventType, startDate, endDate);

        var events = new List<SecurityEvent>();

        // Query audit logs from repository
        if (startDate.HasValue && endDate.HasValue)
        {
            var auditLogs = await _auditLogRepository.GetByDateRangeAsync(
                startDate.Value,
                endDate.Value,
                cancellationToken);

            // Filter by user if specified
            if (!string.IsNullOrWhiteSpace(userId))
            {
                auditLogs = auditLogs.Where(log => log.UserId == userId);
            }

            // Convert AuditLog entities to SecurityEvent value objects
            // Note: This is a simplified conversion - in production, you'd need to map properly
            foreach (var auditLog in auditLogs)
            {
                // Try to infer SecurityEventType from action
                var inferredEventType = InferSecurityEventType(auditLog.Action);

                // Only include if event type matches (if filter specified)
                if (eventType.HasValue && inferredEventType != eventType.Value)
                {
                    continue;
                }

                var securityEvent = SecurityEvent.Create(
                    eventType: inferredEventType,
                    timestamp: auditLog.Timestamp,
                    userId: auditLog.UserId,
                    ipAddress: auditLog.IpAddress,
                    resource: auditLog.EntityType,
                    action: auditLog.Action,
                    succeeded: true, // Audit logs typically only log successful operations, failures are logged separately
                    details: auditLog.Changes?.ToString());

                events.Add(securityEvent);
            }
        }

        _logger.LogInformation("Queried {Count} security events", events.Count);

        return events;
    }

    private SecurityEventType InferSecurityEventType(string action)
    {
        var actionLower = action.ToLowerInvariant();

        if (actionLower.Contains("authentication") || actionLower.Contains("login"))
        {
            return SecurityEventType.AuthenticationFailure;
        }

        if (actionLower.Contains("unauthorized") || actionLower.Contains("forbidden"))
        {
            return SecurityEventType.UnauthorizedAccess;
        }

        if (actionLower.Contains("rate") || actionLower.Contains("limit"))
        {
            return SecurityEventType.RateLimitExceeded;
        }

        if (actionLower.Contains("payment") && actionLower.Contains("suspicious"))
        {
            return SecurityEventType.SuspiciousPaymentPattern;
        }

        return SecurityEventType.Other;
    }
}

