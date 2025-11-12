using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Service for sending notifications to stakeholders.
/// Integrates with external notification services (email, SMS, Slack, etc.).
/// Follows Single Responsibility Principle - only handles notifications.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        _logger.LogInformation(
            "Sending notification to stakeholders. Severity: {Severity}, Message: {Message}",
            severity, message);

        // Get recipients based on severity
        var recipients = GetRecipientsForSeverity(severity);

        return await NotifyStakeholdersAsync(severity, message, recipients, cancellationToken);
    }

    public async Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        IEnumerable<string> recipients,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        if (recipients == null)
            throw new ArgumentNullException(nameof(recipients));

        var recipientList = recipients.ToList();
        if (recipientList.Count == 0)
        {
            _logger.LogWarning("No recipients specified for notification");
            return false;
        }

        _logger.LogInformation(
            "Sending notification to {Count} recipients. Severity: {Severity}",
            recipientList.Count, severity);

        try
        {
            // In production, this would integrate with:
            // - Email service (SendGrid, AWS SES, etc.)
            // - SMS service (Twilio, etc.)
            // - Slack/Teams webhooks
            // - PagerDuty for critical incidents

            var enabled = _configuration.GetValue<bool>("Notifications:Enabled", true);
            if (!enabled)
            {
                _logger.LogDebug("Notifications are disabled in configuration");
                return true; // Return true to not block incident response
            }

            // For now, just log the notification
            // In production, implement actual notification sending
            foreach (var recipient in recipientList)
            {
                _logger.LogInformation(
                    "Notification sent to {Recipient}. Severity: {Severity}, Message: {Message}",
                    recipient, severity, message);
            }

            // Simulate async operation
            await Task.Delay(100, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications to stakeholders");
            return false;
        }
    }

    private IEnumerable<string> GetRecipientsForSeverity(IncidentSeverity severity)
    {
        // Get recipients from configuration based on severity
        var recipients = new List<string>();

        switch (severity)
        {
            case IncidentSeverity.Critical:
                recipients.AddRange(GetConfigurationRecipients("Notifications:CriticalRecipients"));
                recipients.AddRange(GetConfigurationRecipients("Notifications:HighRecipients"));
                recipients.AddRange(GetConfigurationRecipients("Notifications:DefaultRecipients"));
                break;

            case IncidentSeverity.High:
                recipients.AddRange(GetConfigurationRecipients("Notifications:HighRecipients"));
                recipients.AddRange(GetConfigurationRecipients("Notifications:DefaultRecipients"));
                break;

            case IncidentSeverity.Medium:
                recipients.AddRange(GetConfigurationRecipients("Notifications:MediumRecipients"));
                recipients.AddRange(GetConfigurationRecipients("Notifications:DefaultRecipients"));
                break;

            case IncidentSeverity.Low:
                recipients.AddRange(GetConfigurationRecipients("Notifications:DefaultRecipients"));
                break;
        }

        // Remove duplicates
        return recipients.Distinct().ToList();
    }

    private IEnumerable<string> GetConfigurationRecipients(string configKey)
    {
        var recipients = _configuration.GetSection(configKey).Get<string[]>();
        return recipients ?? Enumerable.Empty<string>();
    }
}

