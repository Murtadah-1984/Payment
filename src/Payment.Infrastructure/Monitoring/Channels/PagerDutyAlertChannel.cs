using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Enums;
using System.Net.Http.Json;
using System.Text.Json;

namespace Payment.Infrastructure.Monitoring.Channels;

/// <summary>
/// PagerDuty alert channel implementation.
/// Follows Single Responsibility Principle - only handles PagerDuty notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class PagerDutyAlertChannel : IAlertChannel
{
    private readonly PagerDutyAlertChannelOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PagerDutyAlertChannel> _logger;

    public string Name => "PagerDuty";
    public AlertSeverity MinimumSeverity => AlertSeverity.Critical;

    public PagerDutyAlertChannel(
        IOptions<PagerDutyAlertChannelOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<PagerDutyAlertChannel> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        // PagerDuty only triggers for Critical severity
        if (severity != AlertSeverity.Critical)
        {
            _logger.LogDebug(
                "PagerDuty alert skipped - severity {Severity} is below Critical threshold",
                severity);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.IntegrationKey))
        {
            _logger.LogWarning("PagerDuty integration key not configured. Skipping PagerDuty alert.");
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("PagerDutyAlert");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Token token={_options.IntegrationKey}");

            var payload = BuildPagerDutyPayload(severity, title, message, metadata);

            var response = await httpClient.PostAsJsonAsync(
                "https://events.pagerduty.com/v2/enqueue",
                payload,
                ct);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "PagerDuty alert sent. Severity: {Severity}, Title: {Title}",
                severity, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send PagerDuty alert. Severity: {Severity}, Title: {Title}",
                severity, title);
            throw;
        }
    }

    private object BuildPagerDutyPayload(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata)
    {
        var severityLevel = severity switch
        {
            AlertSeverity.Critical => "critical",
            AlertSeverity.High => "error",
            AlertSeverity.Medium => "warning",
            AlertSeverity.Low => "info",
            _ => "info"
        };

        var customDetails = new Dictionary<string, object>
        {
            ["severity"] = severity.ToString(),
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                customDetails[kvp.Key] = kvp.Value ?? "N/A";
            }
        }

        return new
        {
            routing_key = _options.IntegrationKey,
            event_action = "trigger",
            dedup_key = Guid.NewGuid().ToString(),
            payload = new
            {
                summary = title,
                source = "Payment Service",
                severity = severityLevel,
                timestamp = DateTime.UtcNow.ToString("O"),
                component = "Payment Microservice",
                group = "Payment Processing",
                class = "Payment Failure",
                custom_details = customDetails
            }
        };
    }
}

/// <summary>
/// Configuration options for PagerDuty alert channel.
/// </summary>
public class PagerDutyAlertChannelOptions
{
    public string IntegrationKey { get; set; } = string.Empty;
}

