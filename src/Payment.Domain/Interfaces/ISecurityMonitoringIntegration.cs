using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for integrating with external security monitoring tools (SIEM, IDS/IPS, Threat Intelligence).
/// Follows Interface Segregation Principle - focused interface for security monitoring.
/// Implements Dependency Inversion Principle - depends on abstractions.
/// </summary>
public interface ISecurityMonitoringIntegration
{
    /// <summary>
    /// Send a security event to the monitoring system.
    /// </summary>
    Task SendSecurityEventAsync(
        SecurityEvent securityEvent,
        CancellationToken ct = default);

    /// <summary>
    /// Check if an IP address is a known threat.
    /// </summary>
    Task<bool> IsThreatAsync(
        string ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Get threat intelligence data.
    /// </summary>
    Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Check if the integration is healthy and available.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>
/// Represents threat intelligence data from external sources.
/// </summary>
public sealed record ThreatIntelligence(
    string Source,
    string ThreatType,
    string? IpAddress,
    string? Domain,
    string? Description,
    DateTime? FirstSeen,
    DateTime? LastSeen,
    int? ConfidenceScore)
{
    public static ThreatIntelligence Create(
        string source,
        string threatType,
        string? ipAddress = null,
        string? domain = null,
        string? description = null,
        DateTime? firstSeen = null,
        DateTime? lastSeen = null,
        int? confidenceScore = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source cannot be null or empty", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(threatType))
        {
            throw new ArgumentException("ThreatType cannot be null or empty", nameof(threatType));
        }

        return new ThreatIntelligence(
            Source: source,
            ThreatType: threatType,
            IpAddress: ipAddress,
            Domain: domain,
            Description: description,
            FirstSeen: firstSeen,
            LastSeen: lastSeen,
            ConfidenceScore: confidenceScore);
    }
}

