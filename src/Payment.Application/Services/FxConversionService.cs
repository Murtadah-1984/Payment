using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Interfaces;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of FX conversion service.
/// Follows Single Responsibility Principle - orchestrates currency conversion using external Forex API.
/// Depends on IForexApiClient abstraction (Dependency Inversion Principle).
/// </summary>
public sealed class FxConversionService : IFxConversionService
{
    private readonly IForexApiClient _forexApiClient;
    private readonly ILogger<FxConversionService> _logger;

    public FxConversionService(
        IForexApiClient forexApiClient,
        ILogger<FxConversionService> logger)
    {
        _forexApiClient = forexApiClient ?? throw new ArgumentNullException(nameof(forexApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FxConversionResultDto> ConvertAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency))
            throw new ArgumentException("From currency cannot be null or empty", nameof(fromCurrency));
        
        if (string.IsNullOrWhiteSpace(toCurrency))
            throw new ArgumentException("To currency cannot be null or empty", nameof(toCurrency));
        
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        _logger.LogInformation(
            "Converting {Amount} from {FromCurrency} to {ToCurrency}",
            amount, fromCurrency, toCurrency);

        var (rate, convertedAmount) = await _forexApiClient.GetExchangeRateAsync(
            fromCurrency,
            toCurrency,
            amount,
            cancellationToken);

        var result = new FxConversionResultDto(
            OriginalAmount: amount,
            ConvertedAmount: convertedAmount,
            FromCurrency: fromCurrency.ToUpperInvariant(),
            ToCurrency: toCurrency.ToUpperInvariant(),
            Rate: rate,
            Timestamp: DateTime.UtcNow);

        _logger.LogInformation(
            "FX conversion completed: {OriginalAmount} {FromCurrency} → {ConvertedAmount} {ToCurrency} (Rate: {Rate})",
            result.OriginalAmount, result.FromCurrency, result.ConvertedAmount, result.ToCurrency, result.Rate);

        return result;
    }
}

