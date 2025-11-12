using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.External;

/// <summary>
/// Implementation of Forex API client using exchangerate.host API.
/// Follows Single Responsibility Principle - handles external Forex API communication only.
/// Implements IForexApiClient interface from Domain layer (Dependency Inversion).
/// </summary>
public sealed class ForexApiClient : IForexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ForexApiClient> _logger;
    private readonly IConfiguration _configuration;

    public ForexApiClient(
        HttpClient httpClient,
        ILogger<ForexApiClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<(decimal Rate, decimal ConvertedAmount)> GetExchangeRateAsync(
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

        // If currencies are the same, no conversion needed
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Currencies are the same ({Currency}), returning rate 1.0", fromCurrency);
            return (1.0m, amount);
        }

        try
        {
            var apiKey = _configuration["Forex:ApiKey"];
            var baseUrl = _configuration["Forex:BaseUrl"] ?? "https://api.exchangerate.host";
            
            // Build API URL
            var url = $"{baseUrl}/convert?from={fromCurrency.ToUpperInvariant()}&to={toCurrency.ToUpperInvariant()}&amount={amount}";
            
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                url += $"&apikey={apiKey}";
            }

            _logger.LogInformation(
                "Fetching exchange rate from {FromCurrency} to {ToCurrency} for amount {Amount}",
                fromCurrency, toCurrency, amount);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            // Parse response from exchangerate.host API
            // Response format: { "success": true, "query": {...}, "info": { "rate": 1.234 }, "result": 123.4 }
            if (!jsonDoc.RootElement.TryGetProperty("success", out var successElement) ||
                !successElement.GetBoolean())
            {
                var errorMessage = jsonDoc.RootElement.TryGetProperty("error", out var errorElement)
                    ? errorElement.GetString() ?? "Unknown error"
                    : "API returned success=false";
                
                _logger.LogError("Forex API returned error: {Error}", errorMessage);
                throw new InvalidOperationException($"Forex API error: {errorMessage}");
            }

            var rate = jsonDoc.RootElement.GetProperty("info").GetProperty("rate").GetDecimal();
            var convertedAmount = jsonDoc.RootElement.GetProperty("result").GetDecimal();

            _logger.LogInformation(
                "Exchange rate retrieved: {FromCurrency} → {ToCurrency} = {Rate}, Converted: {ConvertedAmount}",
                fromCurrency, toCurrency, rate, convertedAmount);

            return (rate, convertedAmount);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching exchange rate from {FromCurrency} to {ToCurrency}",
                fromCurrency, toCurrency);
            throw new InvalidOperationException($"Failed to fetch exchange rate: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while processing Forex API response");
            throw new InvalidOperationException($"Failed to parse Forex API response: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching exchange rate from {FromCurrency} to {ToCurrency}",
                fromCurrency, toCurrency);
            throw;
        }
    }
}

