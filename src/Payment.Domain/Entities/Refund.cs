using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Entities;

/// <summary>
/// Represents a refund transaction for a payment.
/// Supports full and partial refunds with audit trail.
/// </summary>
public class Refund : Entity
{
    private Refund() { } // EF Core

    public Refund(
        Guid id,
        PaymentId paymentId,
        Amount amount,
        Currency currency,
        string reason,
        PaymentStatus status = PaymentStatus.Pending)
    {
        if (amount.Value <= 0)
        {
            throw new ArgumentException("Refund amount must be greater than zero", nameof(amount));
        }

        Id = id;
        PaymentId = paymentId;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        Status = status;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public PaymentId PaymentId { get; private set; }
    public Amount Amount { get; private set; }
    public Currency Currency { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? RefundTransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Process(string refundTransactionId)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot process refund with status {Status}");
        }

        RefundTransactionId = refundTransactionId;
        Status = PaymentStatus.Succeeded;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException("Cannot fail a completed refund");
        }

        Status = PaymentStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}

