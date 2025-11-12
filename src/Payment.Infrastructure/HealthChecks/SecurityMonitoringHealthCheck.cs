using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.HealthChecks;

/// <summary>
/// Health check for security monitoring integrations.
/// Follows Single Responsibility Principle - only checks security monitoring health.
/// </summary>
public class SecurityMonitoringHealthCheck : IHealthCheck
{
    private readonly ISecurityMonitoringIntegration _securityMonitoring;

    public SecurityMonitoringHealthCheck(ISecurityMonitoringIntegration securityMonitoring)
    {
        _securityMonitoring = securityMonitoring ?? throw new ArgumentNullException(nameof(securityMonitoring));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _securityMonitoring.IsHealthyAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy("Security monitoring integration is healthy");
            }

            return HealthCheckResult.Unhealthy("Security monitoring integration is unhealthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Security monitoring health check failed",
                ex);
        }
    }
}

