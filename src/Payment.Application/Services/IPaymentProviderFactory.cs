using Payment.Domain.Interfaces;

namespace Payment.Application.Services;

/// <summary>
/// Factory interface for creating payment provider instances.
/// Supports both synchronous and asynchronous creation with feature flag checks.
/// </summary>
public interface IPaymentProviderFactory
{
    /// <summary>
    /// Creates a payment provider synchronously (for backward compatibility).
    /// Note: For new code, prefer CreateAsync to support feature flag checks.
    /// </summary>
    IPaymentProvider Create(string providerName);
    
    /// <summary>
    /// Creates a payment provider asynchronously with feature flag support.
    /// </summary>
    Task<IPaymentProvider> CreateAsync(string providerName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available payment provider names.
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
}

