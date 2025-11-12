using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;

namespace Payment.Infrastructure.Providers;

/// <summary>
/// Decorator for payment providers that adds resilience patterns (Resilience Patterns #8).
/// Implements Circuit Breaker, Retry, and Timeout policies using Polly.
/// Follows Decorator Pattern and Open/Closed Principle.
/// </summary>
public class ResilientPaymentProviderDecorator : IPaymentProvider
{
    private readonly IPaymentProvider _inner;
    private readonly IAsyncPolicy<PaymentResult> _policy;
    private readonly ILogger<ResilientPaymentProviderDecorator> _logger;
    private readonly IMetricsRecorder? _metricsRecorder;

    public string ProviderName => _inner.ProviderName;

    public ResilientPaymentProviderDecorator(
        IPaymentProvider inner,
        ILogger<ResilientPaymentProviderDecorator> logger,
        IMetricsRecorder? metricsRecorder = null)
    {
        _inner = inner;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
        _policy = CreatePolicy();
    }

    private IAsyncPolicy<PaymentResult> CreatePolicy()
    {
        // Timeout: 30 seconds
        var timeoutPolicy = Policy.TimeoutAsync<PaymentResult>(
            TimeSpan.FromSeconds(30),
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogWarning("Payment provider {ProviderName} timed out after {Timeout}s", 
                    ProviderName, timespan.TotalSeconds);
                return Task.CompletedTask;
            });

        // Retry: 3 times with exponential backoff
        var retryPolicy = Policy<PaymentResult>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .OrResult(result => !result.Success && result.FailureReason?.Contains("temporary", StringComparison.OrdinalIgnoreCase) == true)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} for provider {ProviderName} after {Delay}s due to {Exception}",
                        retryCount, ProviderName, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.FailureReason);
                });

        // Circuit Breaker: Open after 5 failures, stay open for 60 seconds
        var circuitBreakerPolicy = Policy<PaymentResult>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .OrResult(result => !result.Success)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(60),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for provider {ProviderName} for {Duration}s due to {Exception}",
                        ProviderName, duration.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.FailureReason);
                    _metricsRecorder?.UpdateCircuitBreakerState(ProviderName, "open");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for provider {ProviderName}", ProviderName);
                    _metricsRecorder?.UpdateCircuitBreakerState(ProviderName, "closed");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for provider {ProviderName}", ProviderName);
                    _metricsRecorder?.UpdateCircuitBreakerState(ProviderName, "half-open");
                });

        // Combine policies: Timeout -> Retry -> Circuit Breaker
        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _policy.ExecuteAsync(
                async () => await _inner.ProcessPaymentAsync(request, cancellationToken));
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open for provider {ProviderName}", ProviderName);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Payment provider {ProviderName} is temporarily unavailable. Please try again later.",
                ProviderMetadata: null);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Timeout exceeded for provider {ProviderName}", ProviderName);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Payment processing timed out for provider {ProviderName}.",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in resilient payment provider decorator for {ProviderName}", ProviderName);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"An unexpected error occurred: {ex.Message}",
                ProviderMetadata: null);
        }
    }
}

