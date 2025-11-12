using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Security.Integrations;

/// <summary>
/// Generic SIEM integration for forwarding security events.
/// Supports standard SIEM formats (CEF, JSON, Syslog).
/// Follows Single Responsibility Principle - only handles SIEM integration.
/// </summary>
public class GenericSiemIntegration : SecurityMonitoringIntegrationBase
{
    private readonly HttpClient _httpClient;
    private readonly GenericSiemOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public GenericSiemIntegration(
        HttpClient httpClient,
        IOptions<GenericSiemOptions> options,
        ILogger<GenericSiemIntegration> logger)
        : base(logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new ArgumentException("SIEM endpoint cannot be null or empty", nameof(options));
        }

        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    protected override async Task SendSecurityEventInternalAsync(
        SecurityEvent securityEvent,
        CancellationToken ct)
    {
        var siemEvent = new
        {
            timestamp = securityEvent.Timestamp,
            eventType = securityEvent.EventType.ToString(),
            userId = securityEvent.UserId,
            ipAddress = securityEvent.IpAddress,
            resource = securityEvent.Resource,
            action = securityEvent.Action,
            succeeded = securityEvent.Succeeded,
            details = securityEvent.Details,
            source = "PaymentMicroservice",
            severity = GetSeverity(securityEvent.EventType)
        };

        var response = await _httpClient.PostAsJsonAsync(
            _options.EventsEndpoint ?? "/api/events",
            siemEvent,
            _jsonOptions,
            ct);

        response.EnsureSuccessStatusCode();

        Logger.LogDebug(
            "Security event {EventType} sent to SIEM at {Endpoint}",
            securityEvent.EventType,
            _options.Endpoint);
    }

    protected override async Task<bool> IsThreatInternalAsync(
        string ipAddress,
        CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            $"{_options.ThreatCheckEndpoint ?? "/api/threats"}/ip/{ipAddress}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning(
                "Threat check for IP {IpAddress} returned status {StatusCode}",
                ipAddress,
                response.StatusCode);
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<ThreatCheckResult>(_jsonOptions, ct);
        return result?.IsThreat ?? false;
    }

    protected override async Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceInternalAsync(
        CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            _options.ThreatIntelligenceEndpoint ?? "/api/threat-intelligence",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning(
                "Threat intelligence request returned status {StatusCode}",
                response.StatusCode);
            return Enumerable.Empty<ThreatIntelligence>();
        }

        var threats = await response.Content.ReadFromJsonAsync<IEnumerable<ThreatIntelligenceDto>>(
            _jsonOptions,
            ct);

        return threats?.Select(t => new ThreatIntelligence(
            Source: t.Source ?? "SIEM",
            ThreatType: t.ThreatType ?? "Unknown",
            IpAddress: t.IpAddress,
            Domain: t.Domain,
            Description: t.Description,
            FirstSeen: t.FirstSeen,
            LastSeen: t.LastSeen,
            ConfidenceScore: t.ConfidenceScore)) ?? Enumerable.Empty<ThreatIntelligence>();
    }

    protected override async Task<bool> CheckHealthInternalAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                _options.HealthEndpoint ?? "/health",
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Health check failed for SIEM integration");
            return false;
        }
    }

    private static string GetSeverity(SecurityEventType eventType)
    {
        return eventType switch
        {
            SecurityEventType.AuthenticationFailure => "Low",
            SecurityEventType.RateLimitExceeded => "Low",
            SecurityEventType.SuspiciousAuthentication => "Medium",
            SecurityEventType.UnauthorizedAccess => "High",
            SecurityEventType.SuspiciousPaymentPattern => "Medium",
            SecurityEventType.CredentialCompromise => "Critical",
            SecurityEventType.DataBreach => "Critical",
            SecurityEventType.MaliciousPayload => "High",
            SecurityEventType.DDoS => "High",
            _ => "Medium"
        };
    }

    private sealed record ThreatCheckResult(bool IsThreat, string? Reason);

    private sealed record ThreatIntelligenceDto(
        string? Source,
        string? ThreatType,
        string? IpAddress,
        string? Domain,
        string? Description,
        DateTime? FirstSeen,
        DateTime? LastSeen,
        int? ConfidenceScore);
}

/// <summary>
/// Configuration options for generic SIEM integration.
/// </summary>
public class GenericSiemOptions
{
    public const string SectionName = "SecurityMonitoring:GenericSiem";

    public string Endpoint { get; set; } = string.Empty;
    public string? EventsEndpoint { get; set; }
    public string? ThreatCheckEndpoint { get; set; }
    public string? ThreatIntelligenceEndpoint { get; set; }
    public string? HealthEndpoint { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? ApiKey { get; set; }
}

