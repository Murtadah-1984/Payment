using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Security.Integrations;

/// <summary>
/// No-op implementation of security monitoring integration.
/// Used when no external monitoring tools are configured.
/// Follows Null Object pattern.
/// </summary>
public class NoOpSecurityMonitoringIntegration : ISecurityMonitoringIntegration
{
    private readonly ILogger<NoOpSecurityMonitoringIntegration> _logger;

    public NoOpSecurityMonitoringIntegration(ILogger<NoOpSecurityMonitoringIntegration> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendSecurityEventAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        _logger.LogDebug("No-op: Security event {EventType} not sent (no monitoring integration configured)", securityEvent.EventType);
        return Task.CompletedTask;
    }

    public Task<bool> IsThreatAsync(string ipAddress, CancellationToken ct = default)
    {
        _logger.LogDebug("No-op: Threat check for IP {IpAddress} (no monitoring integration configured)", ipAddress);
        return Task.FromResult(false);
    }

    public Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("No-op: Threat intelligence request (no monitoring integration configured)");
        return Task.FromResult(Enumerable.Empty<ThreatIntelligence>());
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // No-op is always "healthy" since it doesn't depend on external services
        return Task.FromResult(true);
    }
}

