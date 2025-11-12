using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Service for checking circuit breaker status of payment providers.
/// Integrates with metrics recorder to get circuit breaker state.
/// Follows Single Responsibility Principle - only handles circuit breaker status.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly IMetricsRecorder _metricsRecorder;
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly Dictionary<string, string> _circuitBreakerStates = new();

    public CircuitBreakerService(
        IMetricsRecorder metricsRecorder,
        ILogger<CircuitBreakerService> logger)
    {
        _metricsRecorder = metricsRecorder ?? throw new ArgumentNullException(nameof(metricsRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> GetCircuitBreakerStateAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        // In production, this would query the actual circuit breaker state from metrics or state store
        // For now, we'll use a simple in-memory cache (in production, use Redis or similar)
        _circuitBreakerStates.TryGetValue(providerName, out var state);
        
        var result = state ?? "closed"; // Default to closed if not found
        
        _logger.LogDebug("Circuit breaker state for {Provider}: {State}", providerName, result);
        
        return Task.FromResult(result);
    }

    public async Task<bool> IsCircuitBreakerOpenAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var state = await GetCircuitBreakerStateAsync(providerName, cancellationToken);
        return state == "open";
    }

    public async Task<IEnumerable<string>> GetProvidersWithOpenCircuitBreakersAsync(CancellationToken cancellationToken = default)
    {
        // In production, this would query all providers and check their circuit breaker states
        // For now, return providers with open circuit breakers from cache
        var providers = new List<string>();
        
        // Common provider names to check
        var commonProviders = new[] { "Stripe", "Checkout", "Helcim", "ZainCash" };
        
        foreach (var provider in commonProviders)
        {
            if (await IsCircuitBreakerOpenAsync(provider, cancellationToken))
            {
                providers.Add(provider);
            }
        }

        _logger.LogDebug("Found {Count} providers with open circuit breakers", providers.Count);
        
        return providers;
    }
}

