namespace Payment.Domain.Entities;

/// <summary>
/// Entity representing an idempotent request to prevent duplicate payment operations.
/// Stores the idempotency key, associated payment ID, and request hash for validation.
/// </summary>
public class IdempotentRequest : Entity
{
    private IdempotentRequest() { } // EF Core

    public IdempotentRequest(
        string idempotencyKey,
        Guid paymentId,
        string requestHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty", nameof(paymentId));
        }

        if (string.IsNullOrWhiteSpace(requestHash))
        {
            throw new ArgumentException("Request hash cannot be null or empty", nameof(requestHash));
        }

        IdempotencyKey = idempotencyKey;
        PaymentId = paymentId;
        RequestHash = requestHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid PaymentId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}


