using Payment.Domain.ValueObjects;

namespace Payment.Domain.Entities;

/// <summary>
/// Represents a single account split within a payment (N-way split).
/// Tracks how funds are distributed across SystemOwner, Merchant, Partners, etc.
/// </summary>
public class PaymentSplit : Entity
{
    private PaymentSplit() { } // EF Core

    public PaymentSplit(
        Guid id,
        PaymentId paymentId,
        string accountType,
        string accountIdentifier,
        decimal percentage,
        decimal amount)
    {
        Id = id;
        PaymentId = paymentId;
        AccountType = accountType;
        AccountIdentifier = accountIdentifier;
        Percentage = percentage;
        Amount = amount;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public PaymentId PaymentId { get; private set; }
    public string AccountType { get; private set; } = string.Empty; // SystemOwner, Merchant, Partner, etc.
    public string AccountIdentifier { get; private set; } = string.Empty; // IBAN, wallet address, provider account
    public decimal Percentage { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

