namespace Payment.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing money (amount + currency).
/// Used throughout the domain for type safety and validation.
/// </summary>
public sealed record Money(decimal Amount, string Currency)
{
    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency code cannot be null or empty", nameof(currency));
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency) => new(0m, currency.ToUpperInvariant());

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Cannot add money with different currencies: {Currency} and {other.Currency}");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Cannot subtract money with different currencies: {Currency} and {other.Currency}");
        }

        if (Amount < other.Amount)
        {
            throw new InvalidOperationException("Insufficient amount for subtraction");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        if (factor < 0)
        {
            throw new ArgumentException("Factor cannot be negative", nameof(factor));
        }

        return new Money(Amount * factor, Currency);
    }
}

