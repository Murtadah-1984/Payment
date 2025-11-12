using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Exchange rate service implementation.
/// Supports multi-currency reporting by converting to base currency.
/// Can be extended to fetch rates from external APIs or local tables.
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    private readonly ILogger<ExchangeRateService> _logger;
    private readonly string _baseCurrency;
    private readonly Dictionary<string, decimal> _exchangeRates; // In-memory cache (should use distributed cache in production)

    public ExchangeRateService(ILogger<ExchangeRateService> logger, IConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseCurrency = configuration?["Reporting:BaseCurrency"] ?? "USD";

        // Initialize with sample rates (in production, fetch from external API or database)
        _exchangeRates = new Dictionary<string, decimal>
        {
            { "USD", 1.0m },
            { "EUR", 0.92m },
            { "GBP", 0.79m },
            { "JPY", 150.0m },
            { "CAD", 1.35m },
            { "AUD", 1.52m },
            { "CHF", 0.88m },
            { "CNY", 7.2m },
            { "INR", 83.0m },
            { "BRL", 4.95m }
        };

        _logger.LogInformation("ExchangeRateService initialized with base currency: {BaseCurrency}", _baseCurrency);
    }

    public string GetBaseCurrency() => _baseCurrency;

    public Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
        {
            throw new ArgumentException("Currency codes cannot be null or empty");
        }

        var fromUpper = fromCurrency.ToUpperInvariant();
        var toUpper = toCurrency.ToUpperInvariant();

        if (fromUpper == toUpper)
        {
            return Task.FromResult(1.0m);
        }

        // If converting to base currency, use direct rate
        if (toUpper == _baseCurrency)
        {
            if (_exchangeRates.TryGetValue(fromUpper, out var rate))
            {
                return Task.FromResult(1.0m / rate);
            }
        }

        // If converting from base currency, use direct rate
        if (fromUpper == _baseCurrency)
        {
            if (_exchangeRates.TryGetValue(toUpper, out var rate))
            {
                return Task.FromResult(rate);
            }
        }

        // Cross-currency conversion via base currency
        var fromRate = _exchangeRates.GetValueOrDefault(fromUpper, 1.0m);
        var toRate = _exchangeRates.GetValueOrDefault(toUpper, 1.0m);
        var exchangeRate = toRate / fromRate;

        _logger.LogDebug("Exchange rate {FromCurrency} to {ToCurrency}: {Rate}", fromCurrency, toCurrency, exchangeRate);

        return Task.FromResult(exchangeRate);
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative", nameof(amount));
        }

        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        return Math.Round(amount * rate, 2);
    }
}

