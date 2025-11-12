namespace Payment.Domain.Entities;

/// <summary>
/// Audit log entity for compliance and security tracking (Audit Logging #7).
/// Follows Single Responsibility Principle - only responsible for audit data.
/// </summary>
public class AuditLog : Entity
{
    private AuditLog() { } // EF Core

    public AuditLog(
        Guid id,
        string userId,
        string action,
        string entityType,
        Guid entityId,
        string? ipAddress,
        string? userAgent,
        Dictionary<string, object>? changes = null)
    {
        Id = id;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Changes = changes ?? new Dictionary<string, object>();
        Timestamp = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty; // PaymentCreated, PaymentRefunded, etc.
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public Dictionary<string, object> Changes { get; private set; } = new();
    public DateTime Timestamp { get; private set; }
}

