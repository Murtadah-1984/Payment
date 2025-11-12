namespace Payment.Domain.Interfaces;

/// <summary>
/// Service for currency exchange rate conversion.
/// Supports multi-currency reporting by converting to base currency.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Gets the exchange rate from source currency to target currency.
    /// </summary>
    Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount from source currency to target currency.
    /// </summary>
    Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the base currency for reporting (e.g., USD).
    /// </summary>
    string GetBaseCurrency();
}

