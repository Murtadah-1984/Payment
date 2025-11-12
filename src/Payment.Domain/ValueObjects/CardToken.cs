namespace Payment.Domain.ValueObjects;

/// <summary>
/// Represents a tokenized card information for PCI DSS compliance.
/// NEVER stores full card numbers, CVV, expiration dates, or PINs.
/// Uses tokenization from payment providers instead.
/// </summary>
public sealed record CardToken
{
    public string Token { get; }
    public string Last4Digits { get; }
    public string CardBrand { get; } // Visa, Mastercard, Amex, etc.

    public CardToken(string token, string last4Digits, string cardBrand)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Card token cannot be empty", nameof(token));
        
        if (string.IsNullOrWhiteSpace(last4Digits))
            throw new ArgumentException("Last 4 digits cannot be empty", nameof(last4Digits));
        
        if (last4Digits.Length != 4 || !last4Digits.All(char.IsDigit))
            throw new ArgumentException("Last 4 digits must be exactly 4 digits", nameof(last4Digits));
        
        if (string.IsNullOrWhiteSpace(cardBrand))
            throw new ArgumentException("Card brand cannot be empty", nameof(cardBrand));

        Token = token;
        Last4Digits = last4Digits;
        CardBrand = cardBrand;
    }

    /// <summary>
    /// Creates a masked card display string (e.g., "**** **** **** 1234")
    /// </summary>
    public string ToMaskedString() => $"**** **** **** {Last4Digits}";
}

