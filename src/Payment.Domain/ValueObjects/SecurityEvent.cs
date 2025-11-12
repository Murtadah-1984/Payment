using Payment.Domain.Enums;

namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing a security event.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record SecurityEvent(
    SecurityEventType EventType,
    DateTime Timestamp,
    string? UserId,
    string? IpAddress,
    string Resource,
    string Action,
    bool Succeeded,
    string? Details)
{
    public static SecurityEvent Create(
        SecurityEventType eventType,
        DateTime timestamp,
        string? userId,
        string? ipAddress,
        string resource,
        string action,
        bool succeeded,
        string? details = null)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("Resource cannot be null or empty", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action cannot be null or empty", nameof(action));
        }

        return new SecurityEvent(
            EventType: eventType,
            Timestamp: timestamp,
            UserId: userId,
            IpAddress: ipAddress,
            Resource: resource,
            Action: action,
            Succeeded: succeeded,
            Details: details);
    }
}

