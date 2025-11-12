using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Polly;
using Polly.Retry;

namespace Payment.Infrastructure.Security.Integrations;

/// <summary>
/// Base class for security monitoring integrations.
/// Provides common functionality like retry policies and error handling.
/// Follows Template Method pattern and Open/Closed Principle.
/// </summary>
public abstract class SecurityMonitoringIntegrationBase : ISecurityMonitoringIntegration
{
    protected readonly ILogger Logger;
    protected readonly AsyncRetryPolicy RetryPolicy;

    protected SecurityMonitoringIntegrationBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure retry policy with exponential backoff
        RetryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Logger.LogWarning(
                        exception,
                        "Retry {RetryCount} for security monitoring integration after {Delay}ms",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    public virtual async Task SendSecurityEventAsync(
        SecurityEvent securityEvent,
        CancellationToken ct = default)
    {
        try
        {
            await RetryPolicy.ExecuteAsync(async () =>
            {
                await SendSecurityEventInternalAsync(securityEvent, ct);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Failed to send security event {EventType} to monitoring system after retries",
                securityEvent.EventType);
            throw;
        }
    }

    public virtual async Task<bool> IsThreatAsync(
        string ipAddress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));
        }

        try
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                return await IsThreatInternalAsync(ipAddress, ct);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Failed to check threat status for IP {IpAddress}",
                ipAddress);
            // Return false on error to avoid blocking legitimate traffic
            return false;
        }
    }

    public virtual async Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceAsync(
        CancellationToken ct = default)
    {
        try
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                return await GetThreatIntelligenceInternalAsync(ct);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Failed to get threat intelligence from monitoring system");
            return Enumerable.Empty<ThreatIntelligence>();
        }
    }

    public virtual async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            return await CheckHealthInternalAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Health check failed for security monitoring integration");
            return false;
        }
    }

    /// <summary>
    /// Internal implementation for sending security events.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract Task SendSecurityEventInternalAsync(
        SecurityEvent securityEvent,
        CancellationToken ct);

    /// <summary>
    /// Internal implementation for checking threat status.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract Task<bool> IsThreatInternalAsync(
        string ipAddress,
        CancellationToken ct);

    /// <summary>
    /// Internal implementation for getting threat intelligence.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceInternalAsync(
        CancellationToken ct);

    /// <summary>
    /// Internal implementation for health checks.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract Task<bool> CheckHealthInternalAsync(CancellationToken ct);
}

