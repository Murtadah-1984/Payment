using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Enums;
using System.Net;
using System.Net.Mail;

namespace Payment.Infrastructure.Monitoring.Channels;

/// <summary>
/// Email alert channel implementation.
/// Follows Single Responsibility Principle - only handles email notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class EmailAlertChannel : IAlertChannel
{
    private readonly EmailAlertChannelOptions _options;
    private readonly ILogger<EmailAlertChannel> _logger;

    public string Name => "Email";
    public AlertSeverity MinimumSeverity => AlertSeverity.Low;

    public EmailAlertChannel(
        IOptions<EmailAlertChannelOptions> options,
        ILogger<EmailAlertChannel> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || 
            string.IsNullOrWhiteSpace(_options.FromAddress) ||
            _options.ToAddresses == null || !_options.ToAddresses.Any())
        {
            _logger.LogWarning("Email alert channel not configured properly. Skipping email alert.");
            return;
        }

        try
        {
            using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort);
            smtpClient.EnableSsl = _options.EnableSsl;
            
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                smtpClient.Credentials = new NetworkCredential(
                    _options.Username,
                    _options.Password);
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromAddress),
                Subject = $"[{severity}] {title}",
                Body = BuildEmailBody(severity, title, message, metadata),
                IsBodyHtml = true
            };

            foreach (var toAddress in _options.ToAddresses)
            {
                mailMessage.To.Add(toAddress);
            }

            await smtpClient.SendMailAsync(mailMessage, ct);
            _logger.LogInformation(
                "Email alert sent. Severity: {Severity}, Title: {Title}",
                severity, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email alert. Severity: {Severity}, Title: {Title}",
                severity, title);
            throw;
        }
    }

    private string BuildEmailBody(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata)
    {
        var severityColor = severity switch
        {
            AlertSeverity.Critical => "#dc3545", // Red
            AlertSeverity.High => "#fd7e14",     // Orange
            AlertSeverity.Medium => "#ffc107",   // Yellow
            AlertSeverity.Low => "#0dcaf0",      // Cyan
            _ => "#6c757d"                       // Gray
        };

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .severity-badge {{ 
            display: inline-block; 
            padding: 4px 8px; 
            border-radius: 4px; 
            color: white; 
            font-weight: bold;
            background-color: {severityColor};
        }}
        .metadata {{ margin-top: 20px; }}
        .metadata table {{ border-collapse: collapse; width: 100%; }}
        .metadata th, .metadata td {{ 
            border: 1px solid #ddd; 
            padding: 8px; 
            text-align: left; 
        }}
        .metadata th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <h2><span class=""severity-badge"">{severity}</span> {WebUtility.HtmlEncode(title)}</h2>
    <p>{WebUtility.HtmlEncode(message)}</p>
    {BuildMetadataHtml(metadata)}
    <hr>
    <p><small>Sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</small></p>
</body>
</html>";

        return html;
    }

    private string BuildMetadataHtml(Dictionary<string, object>? metadata)
    {
        if (metadata == null || !metadata.Any())
            return string.Empty;

        var rows = metadata.Select(kvp =>
            $"<tr><th>{WebUtility.HtmlEncode(kvp.Key)}</th><td>{WebUtility.HtmlEncode(kvp.Value?.ToString() ?? "N/A")}</td></tr>");

        return $@"
<div class=""metadata"">
    <h3>Additional Information</h3>
    <table>
        {string.Join("\n", rows)}
    </table>
</div>";
    }
}

/// <summary>
/// Configuration options for email alert channel.
/// </summary>
public class EmailAlertChannelOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public List<string> ToAddresses { get; set; } = new();
}

