namespace Payment.Domain.ValueObjects;

public sealed record Amount(decimal Value)
{
    public static Amount FromDecimal(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(value));
        }

        return new Amount(value);
    }
}

