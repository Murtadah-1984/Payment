using Payment.Domain.Enums;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for sending security-related notifications.
/// Follows Interface Segregation Principle - focused on security notifications only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface ISecurityNotificationService
{
    /// <summary>
    /// Sends a security alert to the security team.
    /// </summary>
    /// <param name="severity">The severity of the security incident.</param>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was sent successfully.</returns>
    Task<bool> SendSecurityAlertAsync(
        SecurityIncidentSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a security alert with additional context.
    /// </summary>
    /// <param name="severity">The severity of the security incident.</param>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="metadata">Additional metadata about the incident.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was sent successfully.</returns>
    Task<bool> SendSecurityAlertAsync(
        SecurityIncidentSeverity severity,
        string title,
        string message,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);
}

