using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Enums;
using System.Net.Http.Json;

namespace Payment.Infrastructure.Monitoring.Channels;

/// <summary>
/// SMS alert channel implementation.
/// Supports Twilio and AWS SNS.
/// Follows Single Responsibility Principle - only handles SMS notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class SmsAlertChannel : IAlertChannel
{
    private readonly SmsAlertChannelOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsAlertChannel> _logger;

    public string Name => "SMS";
    public AlertSeverity MinimumSeverity => AlertSeverity.High;

    public SmsAlertChannel(
        IOptions<SmsAlertChannelOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SmsAlertChannel> logger)
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
        // SMS only for High and Critical severity
        if (severity < AlertSeverity.High)
        {
            _logger.LogDebug(
                "SMS alert skipped - severity {Severity} is below High threshold",
                severity);
            return;
        }

        if (_options.PhoneNumbers == null || !_options.PhoneNumbers.Any())
        {
            _logger.LogWarning("SMS phone numbers not configured. Skipping SMS alert.");
            return;
        }

        try
        {
            if (_options.Provider == "Twilio")
            {
                await SendViaTwilioAsync(severity, title, message, ct);
            }
            else if (_options.Provider == "AWSSNS")
            {
                await SendViaAwsSnsAsync(severity, title, message, ct);
            }
            else
            {
                _logger.LogWarning("Unknown SMS provider: {Provider}", _options.Provider);
                return;
            }

            _logger.LogInformation(
                "SMS alert sent. Severity: {Severity}, Title: {Title}",
                severity, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SMS alert. Severity: {Severity}, Title: {Title}",
                severity, title);
            throw;
        }
    }

    private async Task SendViaTwilioAsync(
        AlertSeverity severity,
        string title,
        string message,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.TwilioAccountSid) ||
            string.IsNullOrWhiteSpace(_options.TwilioAuthToken) ||
            string.IsNullOrWhiteSpace(_options.TwilioFromNumber))
        {
            _logger.LogWarning("Twilio credentials not configured. Skipping SMS alert.");
            return;
        }

        var httpClient = _httpClientFactory.CreateClient("SmsAlert");
        var authValue = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_options.TwilioAccountSid}:{_options.TwilioAuthToken}"));
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

        var smsMessage = $"[{severity}] {title}: {message}";
        if (smsMessage.Length > 160)
        {
            smsMessage = smsMessage.Substring(0, 157) + "...";
        }

        foreach (var phoneNumber in _options.PhoneNumbers)
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new("From", _options.TwilioFromNumber),
                new("To", phoneNumber),
                new("Body", smsMessage)
            };

            var response = await httpClient.PostAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{_options.TwilioAccountSid}/Messages.json",
                new FormUrlEncodedContent(formData),
                ct);

            response.EnsureSuccessStatusCode();
        }
    }

    private async Task SendViaAwsSnsAsync(
        AlertSeverity severity,
        string title,
        string message,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AwsSnsTopicArn))
        {
            _logger.LogWarning("AWS SNS topic ARN not configured. Skipping SMS alert.");
            return;
        }

        // Note: Full AWS SNS implementation would require AWS SDK
        // This is a simplified version - in production, use AWS SDK for .NET
        _logger.LogInformation(
            "AWS SNS SMS sending would be implemented here. Topic: {TopicArn}, Message: {Message}",
            _options.AwsSnsTopicArn,
            $"[{severity}] {title}: {message}");

        // TODO: Implement full AWS SNS integration using AWSSDK.SimpleNotificationService
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for SMS alert channel.
/// </summary>
public class SmsAlertChannelOptions
{
    public string Provider { get; set; } = "Twilio"; // "Twilio" or "AWSSNS"
    public List<string> PhoneNumbers { get; set; } = new();

    // Twilio settings
    public string? TwilioAccountSid { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? TwilioFromNumber { get; set; }

    // AWS SNS settings
    public string? AwsSnsTopicArn { get; set; }
    public string? AwsRegion { get; set; }
}

