namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for checking circuit breaker status of payment providers.
/// Follows Interface Segregation Principle - focused on circuit breaker status only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Gets the circuit breaker state for a payment provider.
    /// </summary>
    /// <param name="providerName">The name of the payment provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The circuit breaker state: "closed", "open", or "half-open".</returns>
    Task<string> GetCircuitBreakerStateAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a provider's circuit breaker is open (unavailable).
    /// </summary>
    /// <param name="providerName">The name of the payment provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the circuit breaker is open, false otherwise.</returns>
    Task<bool> IsCircuitBreakerOpenAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all providers with open circuit breakers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of provider names with open circuit breakers.</returns>
    Task<IEnumerable<string>> GetProvidersWithOpenCircuitBreakersAsync(CancellationToken cancellationToken = default);
}

