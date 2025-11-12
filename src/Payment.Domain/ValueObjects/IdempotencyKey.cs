namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing an idempotency key for payment operations.
/// Ensures idempotency by preventing duplicate payments from retries.
/// Follows Value Object pattern - immutable and validated.
/// </summary>
public sealed record IdempotencyKey
{
    public string Value { get; }

    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(value));
        }

        if (value.Length < 16 || value.Length > 128)
        {
            throw new ArgumentException(
                "Idempotency key must be between 16 and 128 characters", 
                nameof(value));
        }

        Value = value;
    }

    public static implicit operator string(IdempotencyKey idempotencyKey) => idempotencyKey.Value;
}


