namespace Payment.Domain.ValueObjects;

public sealed record Currency(string Code)
{
    private static readonly HashSet<string> SupportedCurrencies = new()
    {
        "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "CNY", "INR", "BRL"
    };

    public static Currency FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Currency code cannot be null or empty", nameof(code));
        }

        var upperCode = code.ToUpperInvariant();
        if (!SupportedCurrencies.Contains(upperCode))
        {
            throw new ArgumentException($"Unsupported currency code: {code}", nameof(code));
        }

        return new Currency(upperCode);
    }

    public static Currency USD => new("USD");
    public static Currency EUR => new("EUR");
    public static Currency GBP => new("GBP");
}

