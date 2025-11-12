using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Security;

/// <summary>
/// Service for sending security-related notifications.
/// Follows Single Responsibility Principle - only handles security notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class SecurityNotificationService : ISecurityNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityNotificationService> _logger;

    public SecurityNotificationService(
        IConfiguration configuration,
        ILogger<SecurityNotificationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SendSecurityAlertAsync(
        SecurityIncidentSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        return await SendSecurityAlertAsync(severity, title, message, new Dictionary<string, object>(), cancellationToken);
    }

    public async Task<bool> SendSecurityAlertAsync(
        SecurityIncidentSeverity severity,
        string title,
        string message,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        _logger.LogInformation(
            "Sending security alert. Severity: {Severity}, Title: {Title}",
            severity, title);

        try
        {
            // In production, integrate with notification services (email, Slack, PagerDuty, etc.)
            // For now, we'll log the alert and return success
            
            var alertMessage = $"[{severity}] {title}\n\n{message}";
            
            if (metadata.Count > 0)
            {
                alertMessage += "\n\nMetadata:\n";
                foreach (var kvp in metadata)
                {
                    alertMessage += $"{kvp.Key}: {kvp.Value}\n";
                }
            }

            // Log based on severity
            switch (severity)
            {
                case SecurityIncidentSeverity.Critical:
                    _logger.LogCritical(alertMessage);
                    break;
                case SecurityIncidentSeverity.High:
                    _logger.LogError(alertMessage);
                    break;
                case SecurityIncidentSeverity.Medium:
                    _logger.LogWarning(alertMessage);
                    break;
                case SecurityIncidentSeverity.Low:
                    _logger.LogInformation(alertMessage);
                    break;
            }

            // In production, send to:
            // - Email (security team)
            // - Slack channel
            // - PagerDuty (for critical/high)
            // - SIEM system
            // - Security operations center (SOC)

            _logger.LogInformation("Security alert sent successfully. Severity: {Severity}", severity);

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security alert. Severity: {Severity}, Title: {Title}", severity, title);
            return false;
        }
    }
}

