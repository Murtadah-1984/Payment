using Payment.Application.DTOs;

namespace Payment.Application.Services;

/// <summary>
/// Interface for foreign exchange conversion service.
/// Follows Interface Segregation Principle - single responsibility for currency conversion.
/// </summary>
public interface IFxConversionService
{
    /// <summary>
    /// Converts an amount from one currency to another using real-time exchange rates.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217)</param>
    /// <param name="toCurrency">Target currency code (ISO 4217)</param>
    /// <param name="amount">Amount to convert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>FX conversion result with original and converted amounts, exchange rate, and timestamp</returns>
    Task<FxConversionResultDto> ConvertAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default);
}

