using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Security.Integrations;

/// <summary>
/// Service that coordinates multiple security monitoring integrations.
/// Follows Single Responsibility Principle - coordinates integrations.
/// Implements Facade pattern to simplify interaction with multiple integrations.
/// </summary>
public class SecurityMonitoringService : ISecurityMonitoringIntegration
{
    private readonly IEnumerable<ISecurityMonitoringIntegration> _integrations;
    private readonly ILogger<SecurityMonitoringService> _logger;

    public SecurityMonitoringService(
        IEnumerable<ISecurityMonitoringIntegration> integrations,
        ILogger<SecurityMonitoringService> logger)
    {
        _integrations = integrations ?? throw new ArgumentNullException(nameof(integrations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendSecurityEventAsync(
        SecurityEvent securityEvent,
        CancellationToken ct = default)
    {
        var tasks = _integrations.Select(integration =>
            SendEventToIntegrationAsync(integration, securityEvent, ct));

        await Task.WhenAll(tasks);
    }

    public async Task<bool> IsThreatAsync(
        string ipAddress,
        CancellationToken ct = default)
    {
        // Check all integrations, return true if any reports threat
        var tasks = _integrations.Select(integration =>
            CheckThreatInIntegrationAsync(integration, ipAddress, ct));

        var results = await Task.WhenAll(tasks);
        return results.Any(result => result);
    }

    public async Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceAsync(
        CancellationToken ct = default)
    {
        var tasks = _integrations.Select(integration =>
            GetThreatIntelligenceFromIntegrationAsync(integration, ct));

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // Service is healthy if at least one integration is healthy
        var tasks = _integrations.Select(integration =>
            CheckIntegrationHealthAsync(integration, ct));

        var results = await Task.WhenAll(tasks);
        return results.Any(result => result);
    }

    private async Task SendEventToIntegrationAsync(
        ISecurityMonitoringIntegration integration,
        SecurityEvent securityEvent,
        CancellationToken ct)
    {
        try
        {
            await integration.SendSecurityEventAsync(securityEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send security event to integration {IntegrationType}",
                integration.GetType().Name);
            // Continue with other integrations even if one fails
        }
    }

    private async Task<bool> CheckThreatInIntegrationAsync(
        ISecurityMonitoringIntegration integration,
        string ipAddress,
        CancellationToken ct)
    {
        try
        {
            return await integration.IsThreatAsync(ipAddress, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check threat in integration {IntegrationType}",
                integration.GetType().Name);
            return false; // Return false on error to avoid blocking legitimate traffic
        }
    }

    private async Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceFromIntegrationAsync(
        ISecurityMonitoringIntegration integration,
        CancellationToken ct)
    {
        try
        {
            return await integration.GetThreatIntelligenceAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get threat intelligence from integration {IntegrationType}",
                integration.GetType().Name);
            return Enumerable.Empty<ThreatIntelligence>();
        }
    }

    private async Task<bool> CheckIntegrationHealthAsync(
        ISecurityMonitoringIntegration integration,
        CancellationToken ct)
    {
        try
        {
            return await integration.IsHealthyAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Health check failed for integration {IntegrationType}",
                integration.GetType().Name);
            return false;
        }
    }
}

