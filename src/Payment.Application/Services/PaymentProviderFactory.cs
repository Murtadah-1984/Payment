using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Payment.Domain.Interfaces;

namespace Payment.Application.Services;

/// <summary>
/// Factory for creating payment provider instances.
/// Follows Open/Closed Principle - new providers can be added without modifying this class.
/// Follows Dependency Inversion Principle - depends on IPaymentProvider abstraction.
/// </summary>
public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFeatureManager _featureManager;

    public PaymentProviderFactory(IServiceProvider serviceProvider, IFeatureManager featureManager)
    {
        _serviceProvider = serviceProvider;
        _featureManager = featureManager;
    }

    public async Task<IPaymentProvider> CreateAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        var normalizedName = providerName.Trim();
        
        // Check if NewPaymentProvider feature flag is enabled for new/experimental providers (Feature Flags #17)
        // This allows gradual rollout of new providers
        var isNewProvider = IsNewProvider(normalizedName);
        if (isNewProvider)
        {
            var newProviderEnabled = await _featureManager.IsEnabledAsync("NewPaymentProvider", cancellationToken);
            if (!newProviderEnabled)
            {
                throw new NotSupportedException(
                    $"Payment provider '{providerName}' is a new provider and requires the 'NewPaymentProvider' feature flag to be enabled.");
            }
        }
        
        // Get all registered payment providers and find the one matching the name
        var providers = _serviceProvider.GetServices<IPaymentProvider>();
        var provider = providers.FirstOrDefault(p => 
            string.Equals(p.ProviderName, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            throw new NotSupportedException(
                $"Payment provider '{providerName}' is not supported or not registered. " +
                $"Available providers: {string.Join(", ", GetAvailableProviders())}");
        }

        return provider;
    }

    public IPaymentProvider Create(string providerName)
    {
        // Synchronous version for backward compatibility - uses default cancellation token
        return CreateAsync(providerName, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static bool IsNewProvider(string providerName)
    {
        // Define which providers are considered "new" and require feature flag
        // This can be configured or determined based on provider registration date
        var newProviders = new[] { "Checkout", "Verifone", "Paytabs", "Tap", "TapToPay" };
        return newProviders.Contains(providerName, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        var providers = _serviceProvider.GetServices<IPaymentProvider>();
        return providers.Select(p => p.ProviderName);
    }
}

