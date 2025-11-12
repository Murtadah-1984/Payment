namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for external Forex API client.
/// Follows Dependency Inversion Principle - domain depends on abstraction, not implementation.
/// Infrastructure layer implements this interface.
/// </summary>
public interface IForexApiClient
{
    /// <summary>
    /// Gets the exchange rate between two currencies.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217)</param>
    /// <param name="toCurrency">Target currency code (ISO 4217)</param>
    /// <param name="amount">Amount to convert (used for some APIs that require amount)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate and converted amount</returns>
    Task<(decimal Rate, decimal ConvertedAmount)> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default);
}

