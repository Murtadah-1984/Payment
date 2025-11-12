using Payment.Domain.ValueObjects;

namespace Payment.Domain.Entities;

/// <summary>
/// Append-only audit log for refund operations.
/// Provides forensic-level traceability for dispute resolution.
/// </summary>
public class RefundAuditLog : Entity
{
    private RefundAuditLog() { } // EF Core

    public RefundAuditLog(
        Guid id,
        PaymentId paymentId,
        string action,
        string performedBy,
        string? reason = null)
    {
        Id = id;
        PaymentId = paymentId;
        Action = action;
        PerformedBy = performedBy;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public PaymentId PaymentId { get; private set; } = null!; // Initialized by constructor or EF Core
    public string Action { get; private set; } = string.Empty; // Requested, Approved, Rejected, Completed
    public string PerformedBy { get; private set; } = string.Empty; // User ID, System, etc.
    public string? Reason { get; private set; }
    public DateTime Timestamp { get; private set; }
}

