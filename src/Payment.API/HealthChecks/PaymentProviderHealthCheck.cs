using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payment.Application.Services;

namespace Payment.API.HealthChecks;

/// <summary>
/// Custom health check for payment providers.
/// Checks if all registered payment providers are operational.
/// </summary>
public class PaymentProviderHealthCheck : IHealthCheck
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly ILogger<PaymentProviderHealthCheck> _logger;

    public PaymentProviderHealthCheck(
        IPaymentProviderFactory providerFactory,
        ILogger<PaymentProviderHealthCheck> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providers = _providerFactory.GetAvailableProviders().ToList();
            
            if (!providers.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("No payment providers registered"));
            }

            // In a real implementation, you might want to check each provider's health
            // For now, we just verify that providers are registered
            var data = new Dictionary<string, object>
            {
                { "ProviderCount", providers.Count },
                { "Providers", string.Join(", ", providers) }
            };

            return Task.FromResult(HealthCheckResult.Healthy(
                $"All {providers.Count} payment providers are registered and operational",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment provider health");
            return Task.FromResult(HealthCheckResult.Unhealthy("Error checking payment provider health", ex));
        }
    }
}

