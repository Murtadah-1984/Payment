using Payment.Domain.Enums;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for sending notifications to stakeholders.
/// Follows Interface Segregation Principle - focused on notifications only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification to stakeholders about an incident.
    /// </summary>
    /// <param name="severity">The severity of the incident.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was sent successfully, false otherwise.</returns>
    Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to specific stakeholders.
    /// </summary>
    /// <param name="severity">The severity of the incident.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="recipients">The list of recipient email addresses or identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was sent successfully, false otherwise.</returns>
    Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        IEnumerable<string> recipients,
        CancellationToken cancellationToken = default);
}

