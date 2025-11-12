using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Service for checking circuit breaker status of payment providers.
/// Integrates with metrics recorder to get circuit breaker state.
/// Follows Single Responsibility Principle - only handles circuit breaker status.
/// Stateless by design - uses distributed cache for Kubernetes deployment.
/// </summary>
public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly IMetricsRecorder _metricsRecorder;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CircuitBreakerService> _logger;
    private const string CacheKeyPrefix = "circuitbreaker:";

    public CircuitBreakerService(
        IMetricsRecorder metricsRecorder,
        ICacheService cacheService,
        ILogger<CircuitBreakerService> logger)
    {
        _metricsRecorder = metricsRecorder ?? throw new ArgumentNullException(nameof(metricsRecorder));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetCircuitBreakerStateAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        // Query circuit breaker state from distributed cache (stateless - suitable for Kubernetes)
        var cacheKey = $"{CacheKeyPrefix}{providerName}";
        var cached = await _cacheService.GetAsync<CircuitBreakerState>(cacheKey, cancellationToken);
        
        var result = cached?.State ?? "closed"; // Default to closed if not found
        
        _logger.LogDebug("Circuit breaker state for {Provider}: {State}", providerName, result);
        
        return result;
    }

    public async Task SetCircuitBreakerStateAsync(string providerName, string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be null or empty", nameof(state));

        // Store circuit breaker state in distributed cache (stateless - suitable for Kubernetes)
        var cacheKey = $"{CacheKeyPrefix}{providerName}";
        await _cacheService.SetAsync(cacheKey, new CircuitBreakerState { State = state }, TimeSpan.FromMinutes(10), cancellationToken);
        
        _logger.LogDebug("Circuit breaker state updated for {Provider}: {State}", providerName, state);
    }

    private class CircuitBreakerState
    {
        public string State { get; set; } = string.Empty;
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

