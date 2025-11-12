namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing payment provider information for country-based discovery.
/// Follows Clean Architecture - Domain layer value object with no external dependencies.
/// </summary>
public sealed record PaymentProviderInfo(
    string ProviderName,
    string CountryCode,
    string Currency,
    string PaymentMethod,
    bool IsActive)
{
    /// <summary>
    /// Validates that all required fields are provided and non-empty.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ProviderName) &&
               !string.IsNullOrWhiteSpace(CountryCode) &&
               !string.IsNullOrWhiteSpace(Currency) &&
               !string.IsNullOrWhiteSpace(PaymentMethod) &&
               CountryCode.Length == 2; // ISO 3166-1 alpha-2 country codes
    }
}

