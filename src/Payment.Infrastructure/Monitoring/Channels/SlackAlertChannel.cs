using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Enums;
using System.Net.Http.Json;
using System.Text.Json;

namespace Payment.Infrastructure.Monitoring.Channels;

/// <summary>
/// Slack alert channel implementation.
/// Follows Single Responsibility Principle - only handles Slack notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class SlackAlertChannel : IAlertChannel
{
    private readonly SlackAlertChannelOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackAlertChannel> _logger;

    public string Name => "Slack";
    public AlertSeverity MinimumSeverity => AlertSeverity.Medium;

    public SlackAlertChannel(
        IOptions<SlackAlertChannelOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SlackAlertChannel> logger)
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
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("Slack webhook URL not configured. Skipping Slack alert.");
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("SlackAlert");
            var payload = BuildSlackPayload(severity, title, message, metadata);

            var response = await httpClient.PostAsJsonAsync(_options.WebhookUrl, payload, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Slack alert sent. Severity: {Severity}, Title: {Title}",
                severity, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send Slack alert. Severity: {Severity}, Title: {Title}",
                severity, title);
            throw;
        }
    }

    private object BuildSlackPayload(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata)
    {
        var color = severity switch
        {
            AlertSeverity.Critical => "danger",  // Red
            AlertSeverity.High => "warning",      // Orange
            AlertSeverity.Medium => "#ffc107",   // Yellow
            AlertSeverity.Low => "good",         // Green
            _ => "#6c757d"                       // Gray
        };

        var fields = new List<object>
        {
            new { title = "Severity", value = severity.ToString(), @short = true },
            new { title = "Time", value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true }
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata.Take(10)) // Slack limits fields
            {
                fields.Add(new
                {
                    title = kvp.Key,
                    value = kvp.Value?.ToString() ?? "N/A",
                    @short = true
                });
            }
        }

        return new
        {
            attachments = new[]
            {
                new
                {
                    color = color,
                    title = title,
                    text = message,
                    fields = fields,
                    footer = "Payment Service Alert",
                    ts = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()
                }
            }
        };
    }
}

/// <summary>
/// Configuration options for Slack alert channel.
/// </summary>
public class SlackAlertChannelOptions
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? Username { get; set; } = "Payment Service";
}

