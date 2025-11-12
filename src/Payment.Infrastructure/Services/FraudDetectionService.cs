using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Fraud detection service implementation.
/// Integrates with external fraud detection service via HTTP.
/// Follows Single Responsibility Principle - only handles fraud detection.
/// Stateless by design - suitable for Kubernetes deployment.
/// Implements resilience patterns with Polly for retry and circuit breaker.
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FraudDetectionService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

    public FraudDetectionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FraudDetectionService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure resilience policy (retry + circuit breaker)
        // Retry policy: 3 retries with exponential backoff
        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode && msg.StatusCode != System.Net.HttpStatusCode.BadRequest)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Fraud detection service retry {RetryCount} after {Delay}s. Status: {StatusCode}",
                        retryCount,
                        timespan.TotalSeconds,
                        outcome.Result?.StatusCode ?? outcome.Exception?.GetType().Name);
                });

        // Circuit breaker: Open after 5 failures, stay open for 30 seconds
        var circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError(
                        "Fraud detection service circuit breaker opened for {Duration}s. Status: {StatusCode}",
                        duration.TotalSeconds,
                        outcome.Result?.StatusCode ?? outcome.Exception?.GetType().Name);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Fraud detection service circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Fraud detection service circuit breaker half-open");
                });

        // Combine policies: Retry -> Circuit Breaker
        _resiliencePolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    public async Task<FraudCheckResult> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var baseUrl = _configuration["FraudDetection:BaseUrl"];
        var apiKey = _configuration["FraudDetection:ApiKey"];
        var enabled = _configuration.GetValue<bool>("FraudDetection:Enabled", false);

        // If fraud detection is disabled, return low risk
        if (!enabled || string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogDebug("Fraud detection is disabled or not configured, returning low risk");
            return FraudCheckResult.LowRisk();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Fraud detection API key is missing, returning low risk");
            return FraudCheckResult.LowRisk();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("FraudDetection");
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var payload = new
            {
                amount = request.Amount,
                currency = request.Currency,
                paymentMethod = request.PaymentMethod,
                customerEmail = request.CustomerEmail,
                customerPhone = request.CustomerPhone,
                customerId = request.CustomerId,
                deviceId = request.DeviceId,
                ipAddress = request.IpAddress,
                merchantId = request.MerchantId,
                orderId = request.OrderId,
                projectCode = request.ProjectCode,
                metadata = request.Metadata
            };

            _logger.LogInformation(
                "Checking fraud risk for order {OrderId}, amount {Amount} {Currency}",
                request.OrderId,
                request.Amount,
                request.Currency);

            var response = await _resiliencePolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await client.PostAsJsonAsync("/api/v1/fraud/check", payload, cancellationToken);
                return httpResponse;
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FraudDetectionApiResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);

                if (result == null)
                {
                    _logger.LogWarning("Fraud detection service returned null response, defaulting to low risk");
                    return FraudCheckResult.LowRisk();
                }

                var riskLevel = MapRiskLevel(result.RiskLevel);
                var reasons = result.Reasons ?? Array.Empty<string>();
                var riskScore = result.RiskScore ?? 0.0m;

                _logger.LogInformation(
                    "Fraud check completed for order {OrderId}: Risk={RiskLevel}, Score={RiskScore}",
                    request.OrderId,
                    riskLevel,
                    riskScore);

                return new FraudCheckResult(
                    riskLevel,
                    result.Recommendation ?? "Approve",
                    riskScore,
                    reasons,
                    result.TransactionId);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Fraud detection service returned error status {StatusCode} for order {OrderId}: {Error}",
                    response.StatusCode,
                    request.OrderId,
                    errorContent);

                // On service failure, default to low risk to avoid blocking legitimate transactions
                // This is a business decision - you may want to block on failure in some scenarios
                return FraudCheckResult.LowRisk();
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Fraud detection check timed out for order {OrderId}, defaulting to low risk", request.OrderId);
            return FraudCheckResult.LowRisk();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking fraud for order {OrderId}, defaulting to low risk", request.OrderId);
            // On exception, default to low risk to avoid blocking legitimate transactions
            return FraudCheckResult.LowRisk();
        }
    }

    private static FraudRiskLevel MapRiskLevel(string? riskLevel)
    {
        return riskLevel?.ToUpperInvariant() switch
        {
            "HIGH" => FraudRiskLevel.High,
            "MEDIUM" or "MED" => FraudRiskLevel.Medium,
            "LOW" or _ => FraudRiskLevel.Low
        };
    }

    private sealed record FraudDetectionApiResponse
    {
        public string? RiskLevel { get; init; }
        public decimal? RiskScore { get; init; }
        public string? Recommendation { get; init; }
        public IReadOnlyList<string>? Reasons { get; init; }
        public string? TransactionId { get; init; }
    }
}

