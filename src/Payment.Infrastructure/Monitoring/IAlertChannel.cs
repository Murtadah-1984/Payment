using Payment.Domain.Enums;

namespace Payment.Infrastructure.Monitoring;

/// <summary>
/// Interface for alert notification channels.
/// Follows Interface Segregation Principle - each channel implements this interface.
/// </summary>
public interface IAlertChannel
{
    /// <summary>
    /// Gets the channel name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the minimum severity level this channel supports.
    /// </summary>
    AlertSeverity MinimumSeverity { get; }

    /// <summary>
    /// Sends an alert through this channel.
    /// </summary>
    /// <param name="severity">The alert severity.</param>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default);
}

