namespace Payment.Infrastructure.Monitoring;

/// <summary>
/// Represents an alert acknowledgment.
/// Used to track which alerts have been acknowledged by operators.
/// </summary>
public class AlertAcknowledgment
{
    /// <summary>
    /// Gets or sets the alert key that was acknowledged.
    /// </summary>
    public string AlertKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user who acknowledged the alert.
    /// </summary>
    public string AcknowledgedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the alert was acknowledged.
    /// </summary>
    public DateTime AcknowledgedAt { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the acknowledgment.
    /// </summary>
    public string? Notes { get; set; }
}

