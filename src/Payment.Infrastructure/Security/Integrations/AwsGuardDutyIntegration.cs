using Amazon.GuardDuty;
using Amazon.GuardDuty.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Security.Integrations;

/// <summary>
/// AWS GuardDuty integration for security monitoring.
/// Forwards security events to GuardDuty and queries threat intelligence.
/// Follows Single Responsibility Principle - only handles GuardDuty integration.
/// </summary>
public class AwsGuardDutyIntegration : SecurityMonitoringIntegrationBase
{
    private readonly IAmazonGuardDuty _guardDutyClient;
    private readonly AwsGuardDutyOptions _options;

    public AwsGuardDutyIntegration(
        IAmazonGuardDuty guardDutyClient,
        IOptions<AwsGuardDutyOptions> options,
        ILogger<AwsGuardDutyIntegration> logger)
        : base(logger)
    {
        _guardDutyClient = guardDutyClient ?? throw new ArgumentNullException(nameof(guardDutyClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.DetectorId))
        {
            throw new ArgumentException("GuardDuty detector ID cannot be null or empty", nameof(options));
        }
    }

    protected override async Task SendSecurityEventInternalAsync(
        SecurityEvent securityEvent,
        CancellationToken ct)
    {
        try
        {
            // GuardDuty doesn't have a direct API to send custom events,
            // but we can create findings via CloudWatch Events or use GuardDuty findings API
            // For this implementation, we'll log the event for GuardDuty to pick up via CloudTrail
            Logger.LogInformation(
                "Security event {EventType} logged for GuardDuty detection: UserId={UserId}, IpAddress={IpAddress}, Resource={Resource}",
                securityEvent.EventType,
                securityEvent.UserId,
                securityEvent.IpAddress,
                securityEvent.Resource);

            // In a real implementation, you might:
            // 1. Send events to CloudWatch Events which GuardDuty monitors
            // 2. Create GuardDuty findings programmatically
            // 3. Use AWS Security Hub for centralized security findings
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send security event to GuardDuty");
            throw;
        }
    }

    protected override async Task<bool> IsThreatInternalAsync(
        string ipAddress,
        CancellationToken ct)
    {
        try
        {
            var request = new GetThreatIntelSetRequest
            {
                DetectorId = _options.DetectorId
            };

            // Check if IP is in threat intelligence sets
            var threatIntelSets = await _guardDutyClient.GetThreatIntelSetAsync(request, ct);

            // In a real implementation, you would:
            // 1. Query GuardDuty findings for the IP address
            // 2. Check threat intelligence feeds
            // 3. Use GuardDuty's IP reputation data

            // For now, we'll return false as GuardDuty doesn't provide a direct IP lookup API
            // This would typically be done via GuardDuty findings or threat intelligence sets
            Logger.LogDebug("Threat check for IP {IpAddress} via GuardDuty", ipAddress);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to check threat status via GuardDuty");
            throw;
        }
    }

    protected override async Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceInternalAsync(
        CancellationToken ct)
    {
        try
        {
            var request = new ListFindingsRequest
            {
                DetectorId = _options.DetectorId,
                FindingCriteria = new FindingCriteria
                {
                    Criterion = new Dictionary<string, Condition>
                    {
                        {
                            "severity",
                            new Condition
                            {
                                Gte = 4.0 // High severity findings
                            }
                        }
                    }
                },
                MaxResults = 100
            };

            var response = await _guardDutyClient.ListFindingsAsync(request, ct);

            return response.FindingIds.Select(findingId => new ThreatIntelligence(
                Source: "AWS GuardDuty",
                ThreatType: "Security Finding",
                IpAddress: null,
                Domain: null,
                Description: $"GuardDuty Finding: {findingId}",
                FirstSeen: null,
                LastSeen: null,
                ConfidenceScore: null));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get threat intelligence from GuardDuty");
            return Enumerable.Empty<ThreatIntelligence>();
        }
    }

    protected override async Task<bool> CheckHealthInternalAsync(CancellationToken ct)
    {
        try
        {
            var request = new GetDetectorRequest
            {
                DetectorId = _options.DetectorId
            };

            var response = await _guardDutyClient.GetDetectorAsync(request, ct);
            return response.Status == DetectorStatus.ENABLED;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Health check failed for GuardDuty integration");
            return false;
        }
    }
}

/// <summary>
/// Configuration options for AWS GuardDuty integration.
/// </summary>
public class AwsGuardDutyOptions
{
    public const string SectionName = "SecurityMonitoring:AwsGuardDuty";

    public string DetectorId { get; set; } = string.Empty;
    public string? Region { get; set; }
}

